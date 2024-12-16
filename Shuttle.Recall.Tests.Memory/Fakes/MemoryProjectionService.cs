using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Threading;

namespace Shuttle.Recall.Tests.Memory.Fakes;

/// <summary>
///     This is a naive implementation of a projection service in that we only have the 'recall-fixture' projection
///     to worry about.
/// </summary>
public class MemoryProjectionService : IProjectionService
{
    private readonly IEventProcessorConfiguration _eventProcessorConfiguration;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<int> _managedThreadIds = new();
    private readonly IPrimitiveEventStore _primitiveEventStore;
    private readonly List<PrimitiveEvent> _projectionJournal = new();
    private readonly Dictionary<string, long> _projectionSequenceNumbers = new();

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

            var primitiveEvent = _projectionJournal
                .OrderBy(item => item.SequenceNumber)
                .FirstOrDefault(item => item.SequenceNumber > sequenceNumber);

            if (primitiveEvent == null)
            {
                await GetProjectionJournalAsync(sequenceNumber);

                return null;
            }

            // This is a naive implementation of a round-robin distribution of events to processor threads.
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

    public async Task AcknowledgeAsync(ProjectionEvent projectionEvent)
    {
        _projectionSequenceNumbers[projectionEvent.Projection.Name] = projectionEvent.PrimitiveEvent.SequenceNumber;

        await Task.CompletedTask;
    }

    private async Task GetProjectionJournalAsync(long sequenceNumber)
    {
        // This would get the next batch of primitive event details for the service te return.
        // Include only event types that are handled in this process (IProjectionConfiguration).
        // Once entire batch has completed, update the sequence number for the projections.
        // This is a naive implementation and should be replaced with a more efficient one in actual implementations.

        _projectionJournal.Clear();
        _projectionJournal.AddRange(await _primitiveEventStore.GetCommittedPrimitiveEventsAsync(sequenceNumber));
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