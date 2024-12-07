using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
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
        await _primitiveEventStore.RemoveAggregateAsync(id);
    }

    public async ValueTask<long> SaveAsync(IEnumerable<PrimitiveEvent> primitiveEvents)
    {
        long sequenceNumber = 0;

        var primitiveEventJournals = primitiveEvents.Select(item=>new PrimitiveEventJournal(item)).ToList();

        foreach (var primitiveEventJournal in primitiveEventJournals)
        {
            sequenceNumber = await _primitiveEventStore.AddAsync(primitiveEventJournal);
        }

        if (Transaction.Current != null)
        {
            Transaction.Current.EnlistVolatile(new PrimitiveEventJournalResourceManager(_primitiveEventStore, primitiveEventJournals), EnlistmentOptions.None);
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