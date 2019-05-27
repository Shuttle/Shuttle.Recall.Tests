using System.Collections.Generic;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes
{
    public class MemoryProjectionRepository : IProjectionRepository
    {
        private readonly IDictionary<string, Projection> _projections = new Dictionary<string, Projection>();

        public Projection Find(string name)
        {
            return _projections.ContainsKey(name) ? _projections[name] : null;
        }

        public void Save(Projection projection)
        {
            Guard.AgainstNull(projection, nameof(projection));

            _projections.Remove(projection.Name);
            _projections.Add(projection.Name, projection);
        }

        public void SetSequenceNumber(string projectionName, long sequenceNumber)
        {
        }
    }
}