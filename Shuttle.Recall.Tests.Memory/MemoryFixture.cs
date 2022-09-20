using System;
using System.Collections.Generic;
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

            Dictionary<Guid, List<PrimitiveEvent>> store = new Dictionary<Guid, List<PrimitiveEvent>>();

            services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
            services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
            services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
            services.AddEventStore();

            var serviceProvider = services.BuildServiceProvider();

            var eventStore = serviceProvider.GetRequiredService<IEventStore>();

            RecallFixture.ExerciseStorage(eventStore);
            RecallFixture.ExerciseEventProcessing(serviceProvider.GetRequiredService<IEventProcessor>(), 60);
            RecallFixture.ExerciseStorageRemoval(eventStore);
        }
    }
}