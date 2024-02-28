using System.Collections.Generic;
using System.Threading.Tasks;
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

        public async Task<Projection> FindAsync(string name)
        {
            return await Task.FromResult(Find(name));
        }

        public void Save(Projection projection)
        {
            Guard.AgainstNull(projection, nameof(projection));

            _projections.Remove(projection.Name);
            _projections.Add(projection.Name, projection);

            SetSequenceNumber(projection.Name, projection.SequenceNumber);
        }

        public async Task SaveAsync(Projection projection)
        {
            Save(projection);

            await Task.CompletedTask;
        }

        public void SetSequenceNumber(string projectionName, long sequenceNumber)
        {
            _sequenceNumbers.Remove(projectionName);
            _sequenceNumbers.Add(projectionName, sequenceNumber);
        }

        public async Task SetSequenceNumberAsync(string projectionName, long sequenceNumber)
        {
            SetSequenceNumber(projectionName, sequenceNumber);

            await Task.CompletedTask;
        }

        public long GetSequenceNumber(string projectionName)
        {
            return _sequenceNumbers.ContainsKey(projectionName) ? _sequenceNumbers[projectionName] : 0;
        }

        public async ValueTask<long> GetSequenceNumberAsync(string projectionName)
        {
            return await Task.FromResult(GetSequenceNumber(projectionName));
        }
    }
}