using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory;

public class PrimitiveEventStore : IPrimitiveEventStore
{
    private readonly Dictionary<Guid, List<PrimitiveEvent>> _store = new();
    private static long _sequenceNumber = 1;
    private SemaphoreSlim _lock = new(1, 1);

    public async Task RemoveAsync(Guid id)
    {
        await _lock.WaitAsync();

        try
        {
            _store.Remove(id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask<long> AddAsync(PrimitiveEvent primitiveEvent)
    {
        Guard.AgainstNull(primitiveEvent);

        await _lock.WaitAsync();

        try
        {
            if (!_store.ContainsKey(Guard.AgainstNull(primitiveEvent).Id))
            {
                _store.Add(primitiveEvent.Id, new());
            }

            primitiveEvent.SequenceNumber = _sequenceNumber++;

            _store[primitiveEvent.Id].Add(primitiveEvent);

            return primitiveEvent.SequenceNumber;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<PrimitiveEvent>> GetAsync(Guid id)
    {
        await _lock.WaitAsync();

        try
        {
            return await Task.FromResult(_store.TryGetValue(id, out var value) ? value : new());
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask<long> GetSequenceNumberAsync(Guid id)
    {
        await _lock.WaitAsync();

        try
        {
            return _store.TryGetValue(id, out var value) ? value.Max(item => item.SequenceNumber) : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PrimitiveEvent?> GetNextPrimitiveEventAsync(long sequenceNumber)
    {
        await Task.CompletedTask;

        return _store.Values.SelectMany(list => list).ToList().FirstOrDefault(item => item.SequenceNumber > sequenceNumber);
    }
}