using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Threading;

namespace Shuttle.Recall.Tests.Memory.Fakes;

/// <summary>
/// This is a rather naive implementation of a projection service in that we only have the 'recall-fixture' projection to worry about.
/// </summary>
public class MemoryProjectionService : IProjectionService
{
    private readonly IEventProcessorConfiguration _eventProcessorConfiguration;
    private readonly Dictionary<string, long> _projectionSequenceNumbers = new();
    private readonly IPrimitiveEventStore _primitiveEventStore;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<int> _managedThreadIds = new ();

    public MemoryProjectionService(IPrimitiveEventStore primitiveEventStore, IEventProcessorConfiguration eventProcessorConfiguration)
    {
        _primitiveEventStore = Guard.AgainstNull(primitiveEventStore);
        _eventProcessorConfiguration = Guard.AgainstNull(eventProcessorConfiguration);
    }

    public async Task<ProjectionEvent?> GetProjectionEventAsync(int processorThreadManagedThreadId)
    {
        if (_managedThreadIds.Count == 0)
        {
            return null;
        }

        await _lock.WaitAsync();

        try
        {
           var sequenceNumber = _projectionSequenceNumbers.TryGetValue("recall-fixture", out var number)
                ? number
                : 0;

            var primitiveEvent = await _primitiveEventStore.GetNextPrimitiveEventAsync(sequenceNumber);

            if (primitiveEvent == null)
            {
                return null;
            }

            var managedThreadId = _managedThreadIds[Math.Abs(primitiveEvent.Id.GetHashCode() % _managedThreadIds.Count)];

            return managedThreadId != processorThreadManagedThreadId
                ? null 
                : new(new("recall-fixture", sequenceNumber), primitiveEvent);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetSequenceNumberAsync(string projectionName, long sequenceNumber)
    {
        _projectionSequenceNumbers[projectionName] = sequenceNumber;

        await Task.CompletedTask;
    }

    public async Task StartupAsync(IProcessorThreadPool processorThreadPool)
    {
        Guard.AgainstNull(processorThreadPool);

        await Task.CompletedTask;

        foreach (var projectionConfiguration in _eventProcessorConfiguration.Projections)
        {
            _projectionSequenceNumbers.Add(projectionConfiguration.Name, 0);
        }

        foreach (var processorThread in processorThreadPool.ProcessorThreads)
        {
            _managedThreadIds.Add(processorThread.ManagedThreadId);
        }
    }
}