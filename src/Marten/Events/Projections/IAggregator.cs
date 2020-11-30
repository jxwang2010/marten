using System;
using System.Collections.Generic;

namespace Marten.Events.Projections
{
    [Obsolete("This is getting replaced in v4")]
    public interface IAggregator
    {
        Type AggregateType { get; }
        string Alias { get; }

        bool AppliesTo(EventStream stream);
    }

    [Obsolete("This is getting replaced in v4")]
    public interface IAggregator<T>: IAggregator
    {
        IAggregation<T, TEvent> AggregatorFor<TEvent>();

        T Build(IEnumerable<IEvent> events, IDocumentSession session);

        T Build(IEnumerable<IEvent> events, IDocumentSession session, T state);

        Type[] EventTypes { get; }
    }
}
