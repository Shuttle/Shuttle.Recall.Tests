using System.Collections.Generic;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes
{
    public class MemoryProjectionRepository : IProjectionRepository
    {
        private readonly IDictionary<string, Projection> _projections = new Dictionary<string, Projection>();
        private readonly IDictionary<string, long> _sequenceNumbers = new Dictionary<string, long>();

        public Projection Find(string name)
        {
            return _projections.ContainsKey(name) ? _projections[name] : null;
        }

        public void Save(Projection projection)
        {
            Guard.AgainstNull(projection, nameof(projection));

            _projections.Remove(projection.Name);
            _projections.Add(projection.Name, projection);

            SetSequenceNumber(projection.Name, projection.SequenceNumber);
        }

        public void SetSequenceNumber(string projectionName, long sequenceNumber)
        {
            _sequenceNumbers.Remove(projectionName);
            _sequenceNumbers.Add(projectionName, sequenceNumber);
        }

        public long GetSequenceNumber(string projectionName)
        {
            return _sequenceNumbers.ContainsKey(projectionName) ? _sequenceNumbers[projectionName] : 0;
        }
    }
}