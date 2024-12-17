﻿using System;
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
    private readonly IPrimitiveEventStore _primitiveEventStore;
    private readonly Dictionary<string, List<ThreadPrimitiveEvent>> _projectionThreadPrimitiveEvents = new();
    private int[] _managedThreadIds = [];

    private Projection[] _projections = [];
    private int _roundRobinIndex;

    public MemoryProjectionService(IPrimitiveEventStore primitiveEventStore, IEventProcessorConfiguration eventProcessorConfiguration)
    {
        _primitiveEventStore = Guard.AgainstNull(primitiveEventStore);
        _eventProcessorConfiguration = Guard.AgainstNull(eventProcessorConfiguration);
    }

    public async Task<ProjectionEvent?> GetProjectionEventAsync(int processorThreadManagedThreadId)
    {
        Projection? projection;

        if (_projections.Length == 0)
        {
            return null;
        }

        await _lock.WaitAsync();

        try
        {
            if (_roundRobinIndex >= _projections.Length)
            {
                _roundRobinIndex = 0;
            }

            projection = _projections[_roundRobinIndex++];
        }
        finally
        {
            _lock.Release();
        }

        var projectionThreadPrimitiveEvents = _projectionThreadPrimitiveEvents[projection.Name];

        if (!projectionThreadPrimitiveEvents.Any())
        {
            await GetProjectionJournalAsync(projection);
        }

        if (!projectionThreadPrimitiveEvents.Any())
        {
            return null;
        }

        var threadPrimitiveEvent = projectionThreadPrimitiveEvents.FirstOrDefault(item => item.ManagedThreadId == processorThreadManagedThreadId);

        return threadPrimitiveEvent == null ? null : new ProjectionEvent(projection, threadPrimitiveEvent.PrimitiveEvent);
    }

    public async Task AcknowledgeAsync(ProjectionEvent projectionEvent)
    {
        await _lock.WaitAsync();

        try
        {
            _projectionThreadPrimitiveEvents[projectionEvent.Projection.Name].RemoveAll(item => item.PrimitiveEvent.SequenceNumber == projectionEvent.PrimitiveEvent.SequenceNumber);
        }
        finally
        {
            _lock.Release();
        }

        projectionEvent.Projection.Commit(projectionEvent.PrimitiveEvent.SequenceNumber);

        await Task.CompletedTask;
    }

    private async Task GetProjectionJournalAsync(Projection projection)
    {
        // This would get the next batch of primitive event details for the service te return.
        // Include only event types that are handled in this process (IProjectionConfiguration).
        // Once entire batch has completed, update the sequence number for the projections.
        // This is a naive implementation and should be replaced with a more efficient one in actual implementations.

        await _lock.WaitAsync();

        try
        {
            if (_projectionThreadPrimitiveEvents[projection.Name].Any())
            {
                return;
            }

            foreach (var primitiveEvent in (await _primitiveEventStore.GetCommittedPrimitiveEventsAsync(projection.SequenceNumber + 1)).OrderBy(item => item.SequenceNumber))
            {
                var managedThreadId = _managedThreadIds[Math.Abs((primitiveEvent.CorrelationId ?? primitiveEvent.Id).GetHashCode()) % _managedThreadIds.Length];

                _projectionThreadPrimitiveEvents[projection.Name].Add(new(managedThreadId, primitiveEvent));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StartupAsync(IProcessorThreadPool processorThreadPool)
    {
        Guard.AgainstNull(processorThreadPool);

        await Task.CompletedTask;

        List<Projection> projections = new();

        await _lock.WaitAsync();

        try
        {
            foreach (var projectionConfiguration in _eventProcessorConfiguration.Projections)
            {
                projections.Add(new(projectionConfiguration.Name, 0));
                _projectionThreadPrimitiveEvents.Add(projectionConfiguration.Name, new());
            }

            _projections = projections.ToArray();
            _managedThreadIds = processorThreadPool.ProcessorThreads.Select(item => item.ManagedThreadId).ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }
}