using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory
{
    public class MemoryFixture
    {
        [Test]
        public void Should_be_able_to_exercise_event_store_and_processing()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
            services.AddSingleton<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>();
            services.AddEventStore();

            var serviceProvider = services.BuildServiceProvider();

            var eventStore = serviceProvider.GetRequiredService<IEventStore>();

            RecallFixture.ExerciseStorage(eventStore);
            RecallFixture.ExerciseEventProcessing(serviceProvider.GetRequiredService<IEventProcessor>(), 60);
            RecallFixture.ExerciseStorageRemoval(eventStore);
        }
    }
}