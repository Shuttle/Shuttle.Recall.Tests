﻿using System;
using System.Collections.Generic;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes
{
    public class MemoryPrimitiveEventRepository : IPrimitiveEventRepository
    {
        private static long _sequenceNumber = 1;
        private readonly Dictionary<Guid, List<PrimitiveEvent>> _store;

        public MemoryPrimitiveEventRepository(Dictionary<Guid, List<PrimitiveEvent>> store)
        {
            Guard.AgainstNull(store, nameof(store));

            _store = store;
        }

        public void Remove(Guid id)
        {
            _store.Remove(id);
        }

        public IEnumerable<PrimitiveEvent> Get(Guid id)
        {
            return _store.ContainsKey(id) ? _store[id] : new List<PrimitiveEvent>();
        }

        public long Save(PrimitiveEvent primitiveEvent)
        {
            Guard.AgainstNull(primitiveEvent, nameof(primitiveEvent));

            if (!_store.ContainsKey(primitiveEvent.Id))
            {
                _store.Add(primitiveEvent.Id, new List<PrimitiveEvent>());
            }

            primitiveEvent.SequenceNumber = _sequenceNumber++;

            _store[primitiveEvent.Id].Add(primitiveEvent);

            return _sequenceNumber;
        }

        public long GetSequenceNumber(Guid id)
        {
            return _sequenceNumber;
        }
    }
}