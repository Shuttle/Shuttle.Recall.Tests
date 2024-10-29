﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory;

public class MemoryFixture : RecallFixture
{
    [Test]
    public async Task Should_be_able_to_exercise_event_processing_async()
    {
        var services = new ServiceCollection();

        var store = new Dictionary<Guid, List<PrimitiveEvent>>();

        services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
        services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
        services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
        services.AddEventStore();

        await ExerciseStorageAsync(services);
        await ExerciseEventProcessingAsync(services, handlerTimeoutSeconds: 600);
        await ExerciseStorageRemovalAsync(services);
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_store_async()
    {
        var services = new ServiceCollection();

        var store = new Dictionary<Guid, List<PrimitiveEvent>>();

        services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
        services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
        services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
        services.AddEventStore();

        await ExerciseStorageAsync(services);
        await ExerciseStorageRemovalAsync(services);
    }
}