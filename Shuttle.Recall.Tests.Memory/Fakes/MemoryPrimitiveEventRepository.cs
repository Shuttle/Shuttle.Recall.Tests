using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryPrimitiveEventRepository : IPrimitiveEventRepository
{
    private static long _sequenceNumber = 1;
    private readonly Dictionary<Guid, List<PrimitiveEvent>> _store;

    public MemoryPrimitiveEventRepository(Dictionary<Guid, List<PrimitiveEvent>> store)
    {
        _store = Guard.AgainstNull(store);
    }

    public async Task RemoveAsync(Guid id)
    {
        _store.Remove(id);

        await Task.CompletedTask;
    }

    public async ValueTask<long> SaveAsync(IEnumerable<PrimitiveEvent> primitiveEvents)
    {
        foreach (var primitiveEvent in primitiveEvents)
        {
            if (!_store.ContainsKey(Guard.AgainstNull(primitiveEvent).Id))
            {
                _store.Add(primitiveEvent.Id, new());
            }

            primitiveEvent.SequenceNumber = _sequenceNumber++;

            _store[primitiveEvent.Id].Add(primitiveEvent);
        }

        return await new ValueTask<long>(_sequenceNumber);
    }

    public async Task<IEnumerable<PrimitiveEvent>> GetAsync(Guid id)
    {
        return await Task.FromResult(_store.TryGetValue(id, out var value) ? value : new());
    }

    public async ValueTask<long> GetSequenceNumberAsync(Guid id)
    {
        return await new ValueTask<long>(_sequenceNumber);
    }
}