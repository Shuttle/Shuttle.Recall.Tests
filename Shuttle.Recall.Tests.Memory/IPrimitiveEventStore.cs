using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shuttle.Recall.Tests.Memory;

public interface IPrimitiveEventStore
{
    Task RemoveAsync(Guid id);
    ValueTask<long> AddAsync(PrimitiveEvent primitiveEvent);
    Task<IEnumerable<PrimitiveEvent>> GetAsync(Guid id);
    ValueTask<long> GetSequenceNumberAsync(Guid id);
    Task<PrimitiveEvent?> GetNextPrimitiveEventAsync(long sequenceNumber);
}