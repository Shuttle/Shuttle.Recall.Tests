using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryProjectionService : IProjectionService
{
    private readonly Dictionary<string, long> _sequenceNumbers = new();
    private readonly Dictionary<Guid, List<PrimitiveEvent>> _store;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _managedThreadId;

    public MemoryProjectionService(Dictionary<Guid, List<PrimitiveEvent>> store)
    {
        _store = Guard.AgainstNull(store);
    }

    public async Task<ProjectionEvent?> GetProjectionEventAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (_managedThreadId == 0)
            {
                _managedThreadId = Environment.CurrentManagedThreadId;
            }

            if (_managedThreadId != Environment.CurrentManagedThreadId)
            {
                return null;
            }

            var all = new List<PrimitiveEvent>();

            foreach (var events in _store.Values)
            {
                all.AddRange(events);
            }

            var queryable = all.AsQueryable();



            var sequenceNumber = _sequenceNumbers.TryGetValue("recall-fixture", out var number)
                ? number
                : 0;

            var primitiveEvent = queryable.FirstOrDefault(item => item.SequenceNumber > sequenceNumber);

            if (primitiveEvent == null)
            {
                return null;
            }

            return new(new("recall-fixture", sequenceNumber), primitiveEvent);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetSequenceNumberAsync(string projectionName, long sequenceNumber)
    {
        _sequenceNumbers[projectionName] = sequenceNumber;

        await Task.CompletedTask;
    }
}