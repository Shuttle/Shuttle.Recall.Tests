using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory;

public class MemoryFixture : RecallFixture
{
    [Test]
    public async Task Should_be_able_to_exercise_event_processing_async()
    {
        var services = new ServiceCollection()
            .AddSingleton<IPrimitiveEventStore>(new PrimitiveEventStore())
            .AddSingleton<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>()
            .AddSingleton<IProjectionService, MemoryProjectionService>()
            .AddSingleton<IHostedService, MemoryFixtureHostedService>()
            .AddSingleton<MemoryFixtureStartupObserver>();

        await ExerciseEventProcessingAsync(services, handlerTimeoutSeconds: 600);
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_processing_with_delay_async()
    {
        var services = new ServiceCollection()
            .AddSingleton<IPrimitiveEventStore>(new PrimitiveEventStore())
            .AddSingleton<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>()
            .AddSingleton<IProjectionService, MemoryProjectionService>()
            .AddSingleton<IHostedService, MemoryFixtureHostedService>()
            .AddSingleton<MemoryFixtureStartupObserver>();

        await ExerciseEventProcessingWithDelayAsync(services, handlerTimeoutSeconds: 600);
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_processing_with_failure_async()
    {
        var services = new ServiceCollection()
            .AddSingleton<IPrimitiveEventStore>(new PrimitiveEventStore())
            .AddSingleton<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>()
            .AddSingleton<IProjectionService, MemoryProjectionService>()
            .AddSingleton<IHostedService, MemoryFixtureHostedService>()
            .AddSingleton<MemoryFixtureStartupObserver>();

        await ExerciseEventProcessingWithFailureAsync(services, handlerTimeoutSeconds: 600);
    }

    [Test]
    public async Task Should_be_able_to_exercise_event_store_async()
    {
        var services = new ServiceCollection()
            .AddSingleton<IPrimitiveEventStore>(new PrimitiveEventStore())
            .AddSingleton<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>();

        await ExerciseStorageAsync(services);
    }
}