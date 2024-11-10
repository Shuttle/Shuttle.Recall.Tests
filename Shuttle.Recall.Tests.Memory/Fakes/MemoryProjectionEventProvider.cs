using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryProjectionEventProvider : IProjectionEventProvider
{
    private readonly IProjectionRepository _projectionRepository;
    private readonly Dictionary<Guid, List<PrimitiveEvent>> _store;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _managedThreadId;

    public MemoryProjectionEventProvider(Dictionary<Guid, List<PrimitiveEvent>> store, IProjectionRepository projectionRepository)
    {
        _store = Guard.AgainstNull(store);
        _projectionRepository = Guard.AgainstNull(projectionRepository);
    }

    public async Task<ProjectionEvent?> GetAsync()
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

            var sequenceNumber = await _projectionRepository.GetSequenceNumberAsync("recall-fixture");

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
}