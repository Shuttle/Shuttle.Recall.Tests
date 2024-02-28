using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory
{
    public class MemoryFixture : RecallFixture
    {
        [Test]
        public void Should_be_able_to_exercise_event_store_and_processing()
        {
            Should_be_able_to_exercise_event_store_and_processing_async(true).GetAwaiter().GetResult();
        }

        [Test]
        public async Task Should_be_able_to_exercise_event_store_and_processing_async()
        {
            await Should_be_able_to_exercise_event_store_and_processing_async(false).ConfigureAwait(false);
        }

        private async Task Should_be_able_to_exercise_event_store_and_processing_async(bool sync)
        {
            var services = new ServiceCollection();

            var store = new Dictionary<Guid, List<PrimitiveEvent>>();

            services.AddSingleton<IProjectionRepository, MemoryProjectionRepository>();
            services.AddSingleton<IPrimitiveEventRepository>(new MemoryPrimitiveEventRepository(store));
            services.AddSingleton<IPrimitiveEventQuery>(new MemoryPrimitiveEventQuery(store));
            services.AddEventStore(builder => builder.Options.Asynchronous = !sync);

            var serviceProvider = services.BuildServiceProvider();

            var eventStore = serviceProvider.GetRequiredService<IEventStore>();

            if (sync)
            {
                ExerciseStorage(eventStore);
                ExerciseEventProcessing(serviceProvider.GetRequiredService<IEventProcessor>(), 60);
                ExerciseStorageRemoval(eventStore);
            }
            else
            {
                await ExerciseStorageAsync(eventStore);
                await ExerciseEventProcessingAsync(serviceProvider.GetRequiredService<IEventProcessor>(), 60);
                await ExerciseStorageRemovalAsync(eventStore);
            }
        }
    }
}