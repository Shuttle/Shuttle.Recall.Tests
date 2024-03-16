using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory;

public class MemoryFixture : RecallFixture
{
    [Test]
    public void Should_be_able_to_exercise_event_processing()
    {
        Should_be_able_to_exercise_event_processing_async(true).GetAwaiter().GetResult();
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_processing_async()
    {
        await Should_be_able_to_exercise_event_processing_async(false).ConfigureAwait(false);
    }

    private async Task Should_be_able_to_exercise_event_processing_async(bool sync)
    {
        var services = new ServiceCollection();

        var store = new Dictionary<Guid, List<PrimitiveEvent>>();

        services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
        services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
        services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
        services.AddEventStore(builder => builder.Options.Asynchronous = !sync);

        if (sync)
        {
            ExerciseStorage(services);
            ExerciseEventProcessing(services, 60);
            ExerciseStorageRemoval(services);
        }
        else
        {
            await ExerciseStorageAsync(services);
            await ExerciseEventProcessingAsync(services, 60);
            await ExerciseStorageRemovalAsync(services);
        }
    }

    [Test]
    public void Should_be_able_to_exercise_event_store()
    {
        Should_be_able_to_exercise_event_store_async(true).GetAwaiter().GetResult();
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_store_async()
    {
        await Should_be_able_to_exercise_event_store_async(false).ConfigureAwait(false);
    }

    private async Task Should_be_able_to_exercise_event_store_async(bool sync)
    {
        var services = new ServiceCollection();

        var store = new Dictionary<Guid, List<PrimitiveEvent>>();

        services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
        services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
        services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
        services.AddEventStore(builder => builder.Options.Asynchronous = !sync);

        if (sync)
        {
            ExerciseStorage(services);
            ExerciseStorageRemoval(services);
        }
        else
        {
            await ExerciseStorageAsync(services);
            await ExerciseStorageRemovalAsync(services);
        }
    }
}