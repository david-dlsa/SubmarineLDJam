using System.Collections.Generic;

namespace SubmarineLDJam.Domain.Common
{
    /// <summary>Marker for things that happened in the domain.</summary>
    public interface IDomainEvent { }

    /// <summary>Base for aggregates. Collects domain events until someone pulls them.</summary>
    public abstract class AggregateRoot
    {
        private readonly List<IDomainEvent> _events = new List<IDomainEvent>();

        protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

        public IReadOnlyList<IDomainEvent> PullEvents()
        {
            var copy = _events.ToArray();
            _events.Clear();
            return copy;
        }
    }
}
