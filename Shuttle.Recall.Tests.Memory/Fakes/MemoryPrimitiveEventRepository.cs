using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryPrimitiveEventRepository : IPrimitiveEventRepository
{
    private readonly IPrimitiveEventStore _primitiveEventStore;
    

    public MemoryPrimitiveEventRepository(IPrimitiveEventStore primitiveEventStore)
    {
        _primitiveEventStore = Guard.AgainstNull(primitiveEventStore);
    }

    public async Task RemoveAsync(Guid id)
    {
        await _primitiveEventStore.RemoveAsync(id);
    }

    public async ValueTask<long> SaveAsync(IEnumerable<PrimitiveEvent> primitiveEvents)
    {
        long sequenceNumber = 0;

        foreach (var primitiveEvent in primitiveEvents)
        {
            sequenceNumber = await _primitiveEventStore.AddAsync(primitiveEvent);
        }

        return sequenceNumber;
    }

    public async Task<IEnumerable<PrimitiveEvent>> GetAsync(Guid id)
    {
        return await _primitiveEventStore.GetAsync(id);
    }

    public async ValueTask<long> GetSequenceNumberAsync(Guid id)
    {
        return await _primitiveEventStore.GetSequenceNumberAsync(id);
    }
}