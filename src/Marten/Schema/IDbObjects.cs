﻿using System;
using System.Collections.Generic;
using Marten.Storage;

namespace Marten.Schema
{
    public interface IDbObjects
    {
        /// <summary>
        /// Fetches a list of all of the Marten generated tables
        /// in the database
        /// </summary>
        /// <returns></returns>
        DbObjectName[] SchemaTables();

        /// <summary>
        /// Fetches a list of the Marten document tables
        /// in the database
        /// </summary>
        /// <returns></returns>
        DbObjectName[] DocumentTables();

        /// <summary>
        /// Fetches a list of functions generated by Marten
        /// in the database
        /// </summary>
        /// <returns></returns>
        DbObjectName[] Functions();

        /// <summary>
        /// Checks whether or not a database table exists in the current tenant
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        bool TableExists(DbObjectName table);

        /// <summary>
        /// Query for the Marten related indexes in the current tenant
        /// </summary>
        /// <returns></returns>
        IEnumerable<ActualIndex> AllIndexes();

        /// <summary>
        /// Query for the indexes related to the named table
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        IEnumerable<ActualIndex> IndexesFor(DbObjectName table);

        /// <summary>
        /// Query for the designated FunctionBody
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        FunctionBody DefinitionForFunction(DbObjectName function);

        ForeignKeyConstraint[] AllForeignKeys();
        Table ExistingTableFor(Type type);
    }
}