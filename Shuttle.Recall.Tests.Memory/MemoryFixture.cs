using Castle.Windsor;
using NUnit.Framework;
using Shuttle.Core.Castle;
using Shuttle.Core.Container;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory
{
    public class MemoryFixture
    {
        [Test]
        public void Should_be_able_to_exercise_event_store_and_processing()
        {
            var container = new WindsorComponentContainer(new WindsorContainer());

            container.Register<IProjectionRepository, MemoryProjectionRepository>();
            container.Register<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>();

            EventStore.Register(container, new EventStoreConfiguration());

            RecallFixture.ExerciseEventProcessing(EventStore.Create(container), EventProcessor.Create(container), 60);
        }
    }
}