using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Marten.Schema;
using Marten.Util;

namespace Marten.Events.Projections
{
    /// <summary>
    /// Simple aggregation finder that looks for an aggregate document based on the stream key
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("This will be eliminated in V4 and replaced w/ the new ViewProjection")]
    public class StringIdentifiedAggregateFinder<T>: IAggregationFinder<T> where T : class
    {
        private readonly Action<T, string> _setId;

        public StringIdentifiedAggregateFinder()
        {
            var idMember = DocumentMapping.FindIdMember(typeof(T));

            var docParam = Expression.Parameter(typeof(T), "doc");
            var keyParam = Expression.Parameter(typeof(string), "key");

            var member = Expression.PropertyOrField(docParam, idMember.Name);
            var assign = Expression.Assign(member, keyParam);

            var lambda = Expression.Lambda<Action<T, string>>(assign, docParam, keyParam);

            _setId = ExpressionCompiler.Compile<Action<T, string>>(lambda);
        }

        public T Find(StreamAction stream, IDocumentSession session)
        {
            var returnValue = stream.ActionType == StreamActionType.Start ? New<T>.Instance() : session.Load<T>(stream.Key) ?? New<T>.Instance();
            _setId(returnValue, stream.Key);

            return returnValue;
        }

        public async Task<T> FindAsync(StreamAction stream, IDocumentSession session, CancellationToken token)
        {
            var returnValue = stream.ActionType == StreamActionType.Start ? New<T>.Instance() : await session.LoadAsync<T>(stream.Key, token).ConfigureAwait(false) ?? New<T>.Instance();

            _setId(returnValue, stream.Key);

            return returnValue;
        }

        public Task FetchAllAggregates(IDocumentSession session, StreamAction[] streams, CancellationToken token)
        {
            return session.LoadManyAsync<T>(token, streams.Select(x => x.Key).ToArray());
        }
    }
}
