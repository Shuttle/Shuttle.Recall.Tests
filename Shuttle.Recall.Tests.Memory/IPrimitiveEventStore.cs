using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory;

public interface IPrimitiveEventStore
{
    Task RemoveAggregateAsync(Guid id);
    ValueTask<long> AddAsync(PrimitiveEventJournal primitiveEventJournal);
    Task<IEnumerable<PrimitiveEvent>> GetAsync(Guid id);
    ValueTask<long> GetSequenceNumberAsync(Guid id);
    Task<IEnumerable<PrimitiveEvent>> GetCommittedPrimitiveEventsAsync(long sequenceNumber);
    Task RemoveEventAsync(Guid id, Guid eventId);
    Task<long> GetMaxSequenceNumberAsync();
}