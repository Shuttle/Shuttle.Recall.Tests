using System;
using System.Collections.Generic;
using System.Linq;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes
{
    public class MemoryPrimitiveEventRepository : IPrimitiveEventRepository
    {
        private static long _sequenceNumber = 1;
        private readonly Dictionary<Guid, List<PrimitiveEvent>> _repository = new Dictionary<Guid, List<PrimitiveEvent>>();

        public void Remove(Guid id)
        {
            _repository.Remove(id);
        }

        public IEnumerable<PrimitiveEvent> Get(Guid id)
        {
            return _repository.ContainsKey(id) ? _repository[id] : new List<PrimitiveEvent>();
        }

        public IEnumerable<PrimitiveEvent> Get(long fromSequenceNumber, long toSequenceNumber, IEnumerable<Type> eventTypes)
        {
            var all = new List<PrimitiveEvent>();

            foreach (var events in _repository.Values)
            {
                all.AddRange(events);
            }

            return all.Where(item => item.SequenceNumber >= fromSequenceNumber && item.SequenceNumber <= toSequenceNumber).OrderBy(item => item.SequenceNumber).ToList();
        }

        public void Save(PrimitiveEvent primitiveEvent)
        {
            Guard.AgainstNull(primitiveEvent, nameof(primitiveEvent));

            if (!_repository.ContainsKey(primitiveEvent.Id))
            {
                _repository.Add(primitiveEvent.Id, new List<PrimitiveEvent>());
            }

            primitiveEvent.SequenceNumber = _sequenceNumber++;
            _repository[primitiveEvent.Id].Add(primitiveEvent);
        }
    }
}