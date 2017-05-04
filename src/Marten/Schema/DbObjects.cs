using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Baseline;
using Marten.Storage;
using Marten.Util;

namespace Marten.Schema
{
    public class DbObjects : IDbObjects
    {
        private readonly IConnectionFactory _factory;
        private readonly StorageFeatures _features;

        public DbObjects(IConnectionFactory factory, StorageFeatures features)
        {
            _factory = factory;
            _features = features;
        }

        public DbObjectName[] DocumentTables()
        {
            return SchemaTables().Where(x => x.Name.StartsWith(DocumentMapping.TablePrefix)).ToArray();
        }

        public DbObjectName[] Functions()
        {
            Func<DbDataReader, DbObjectName> transform = r => new DbObjectName(r.GetString(0), r.GetString(1));

            var sql =
                "SELECT specific_schema, routine_name FROM information_schema.routines WHERE type_udt_name != 'trigger' and routine_name like ? and specific_schema = ANY(?);";

            return
                _factory.Fetch(sql, transform, DocumentMapping.MartenPrefix + "%", _features.AllSchemaNames()).ToArray();
        }

        public DbObjectName[] SchemaTables()
        {
            Func<DbDataReader, DbObjectName> transform = r => new DbObjectName(r.GetString(0), r.GetString(1));

            var sql =
                "SELECT schemaname, relname FROM pg_stat_user_tables WHERE relname LIKE ? AND schemaname = ANY(?);";

            var schemaNames = _features.AllSchemaNames();

            var tablePattern = DocumentMapping.MartenPrefix + "%";
            var tables = _factory.Fetch(sql, transform, tablePattern, schemaNames).ToArray();


            return tables;
        }

        public bool TableExists(DbObjectName table)
        {
            var schemaTables = SchemaTables();
            return schemaTables.Contains(table);
        }

        public IEnumerable<ActualIndex> AllIndexes()
        {
            var sql = @"
SELECT
  U.usename                AS user_name,
  ns.nspname               AS schema_name,
  pg_catalog.textin(pg_catalog.regclassout(idx.indrelid :: REGCLASS)) AS table_name,
  i.relname                AS index_name,
  pg_get_indexdef(i.oid) as ddl,
  idx.indisunique          AS is_unique,
  idx.indisprimary         AS is_primary,
  am.amname                AS index_type,
  idx.indkey,
       ARRAY(
           SELECT pg_get_indexdef(idx.indexrelid, k + 1, TRUE)
           FROM
             generate_subscripts(idx.indkey, 1) AS k
           ORDER BY k
       ) AS index_keys,
  (idx.indexprs IS NOT NULL) OR (idx.indkey::int[] @> array[0]) AS is_functional,
  idx.indpred IS NOT NULL AS is_partial
FROM pg_index AS idx
  JOIN pg_class AS i
    ON i.oid = idx.indexrelid
  JOIN pg_am AS am
    ON i.relam = am.oid
  JOIN pg_namespace AS NS ON i.relnamespace = NS.OID
  JOIN pg_user AS U ON i.relowner = U.usesysid
WHERE NOT nspname LIKE 'pg%' AND i.relname like 'mt_%'; -- Excluding system table

";

            Func<DbDataReader, ActualIndex> transform =
                r => new ActualIndex(DbObjectName.Parse(r.GetString(2)), r.GetString(3), r.GetString(4));

            return _factory.Fetch(sql, transform);
        }

        public IEnumerable<ActualIndex> IndexesFor(DbObjectName table)
        {
            return AllIndexes().Where(x => x.Table.Equals(table)).ToArray();
        }

        public FunctionBody DefinitionForFunction(DbObjectName function)
        {
            var sql = @"
SELECT pg_get_functiondef(pg_proc.oid) 
FROM pg_proc JOIN pg_namespace as ns ON pg_proc.pronamespace = ns.oid WHERE ns.nspname = :schema and proname = :function;
SELECT format('DROP FUNCTION %s.%s(%s);'
             ,n.nspname
             ,p.proname
             ,pg_get_function_identity_arguments(p.oid))
FROM   pg_proc p
LEFT JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace 
WHERE  p.proname = :function
AND    n.nspname = :schema;
";

            using (var conn = _factory.Create())
            {
                conn.Open();


                try
                {
                    var cmd = conn.CreateCommand().Sql(sql)
                        .With("schema", function.Schema)
                        .With("function", function.Name);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
   
                        var definition = reader.GetString(0);

                        reader.NextResult();

                        var drops = new List<string>();
                        while (reader.Read())
                        {
                            drops.Add(reader.GetString(0));
                        }

                        return new FunctionBody(function, drops.ToArray(), definition);
                    }
                }
                finally
                {
                    conn.Close();
                }
            }


        }

        public ForeignKeyConstraint[] AllForeignKeys()
        {
            Func<DbDataReader, ForeignKeyConstraint> reader = r => new ForeignKeyConstraint(r.GetString(0), r.GetString(1), r.GetString(2));

            var sql =
                "select constraint_name, constraint_schema, table_name from information_schema.table_constraints where constraint_name LIKE 'mt_%' and constraint_type = 'FOREIGN KEY'";

            return _factory.Fetch(sql, reader).ToArray();
        }

        public Table ExistingTableFor(Type type)
        {
            var mapping = _features.MappingFor(type).As<DocumentMapping>();
            var expected = new DocumentTable(mapping);

            using (var conn = _factory.Create())
            {
                conn.Open();

                return expected.FetchExisting(conn);
            }
        }

        private IEnumerable<TableColumn> findTableColumns(IDocumentMapping documentMapping)
        {
            Func<DbDataReader, TableColumn> transform = r => new TableColumn(r.GetString(0), r.GetString(1));

            var sql =
                "select column_name, data_type from information_schema.columns where table_schema = ? and table_name = ? order by ordinal_position";

            return _factory.Fetch(sql, transform, documentMapping.Table.Schema, documentMapping.Table.Name);
        }

        private string[] primaryKeysFor(IDocumentMapping documentMapping)
        {
            var sql = @"
select a.attname, format_type(a.atttypid, a.atttypmod) as data_type
from pg_index i
join   pg_attribute a on a.attrelid = i.indrelid and a.attnum = ANY(i.indkey)
where attrelid = (select pg_class.oid 
                  from pg_class 
                  join pg_catalog.pg_namespace n ON n.oid = pg_class.relnamespace
                  where n.nspname = ? and relname = ?)
and i.indisprimary; 
";

            return _factory.GetStringList(sql, documentMapping.Table.Schema, documentMapping.Table.Name).ToArray();
        }
    }
}