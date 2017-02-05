using System;
using System.Data.SqlClient;
using System.Threading;
using Autofac;
using NUnit.Framework;
using Shuttle.Core.Autofac;
using Shuttle.Core.Infrastructure;

namespace Shuttle.Recall.Tests
{
    public class RecallFixture
    {
        public static readonly Guid OrderId = new Guid("047FF6FB-FB57-4F63-8795-99F252EDA62F");
        public static readonly Guid OrderProcessId = new Guid("74937207-F430-4746-9F31-4E76EF2FA7E6");

        public static void ExcerciseKeyStore(IKeyStore store)
        {
            Guard.AgainstNull(store, "store");

            var id = OrderId;

            var value = string.Concat("value=", id.ToString());
            var anotherValue = string.Concat("anotherValue=", id.ToString());

            store.Add(id, value);

            Assert.Throws<SqlException>(() => store.Add(id, value), string.Format("Should not be able to add duplicate key / id = {0} / key = '{1}'", id, value));

            var idGet = store.Get(value);

            Assert.IsNotNull(idGet, string.Format("Should be able to retrieve the id of the associated key / id = {0} / key = '{1}'", id, value));
            Assert.AreEqual(id, idGet, string.Format("Should be able to retrieve the correct id of the associated key / id = {0} / key = '{1}' / id retrieved = {2}", id, value, idGet));

            idGet = store.Get(anotherValue);

            Assert.IsNull(idGet, string.Format("Should not be able to get id of non-existent / id = {0} / key = '{1}'", id, anotherValue));

            store.Remove(id);

            idGet = store.Get(value);

            Assert.IsNull(idGet, string.Format("Should be able to remove association using id (was not removed) / id = {0} / key = '{1}'", id, value));

            store.Add(id, value);
            store.Remove(value);

            idGet = store.Get(value);

            Assert.IsNull(idGet, string.Format("Should be able to remove association using key (was not removed) / id = {0} / key = '{1}'", id, value));
        }

        public static void ExerciseEventStore(IEventStore store, IEventProcessor processor, int handlerTimeoutSeconds = 5)
        {
            Guard.AgainstNull(store, "store");
            Guard.AgainstNull(processor, "processor");

            var order = new Order(OrderId);
            var orderProcess = new OrderProcess(OrderProcessId);

            var orderStream = new EventStream(OrderId);
            var orderProcessStream = new EventStream(OrderProcessId);

            orderStream.AddEvent(order.AddItem("t-shirt", 5, 125));
            orderStream.AddEvent(order.AddItem("baked beans", 2, 4.55));
            orderStream.AddEvent(order.AddItem("20L white glossy enamel paint", 1, 700));

            var orderTotal = order.Total();

            store.Save(orderStream);

            orderProcessStream.AddEvent(orderProcess.StartPicking());
            store.Save(orderProcessStream);

            order = new Order(OrderId);
            orderStream = store.Get(OrderId);

            orderStream.Apply(order);

            Assert.AreEqual(orderTotal, order.Total(),
                "The total of the first re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

            orderStream.AddSnapshot(order.GetSnapshotEvent());
            store.Save(orderStream);

            orderProcess = new OrderProcess(OrderProcessId);
            orderProcessStream = store.Get(OrderProcessId);
            orderProcessStream.Apply(orderProcess);

            Assert.IsTrue(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled),
                "Should be able to change status to 'Fulfilled'");

            orderStream.AddEvent(order.AddItem("4kg bag of potatoes", 5, 15.35));

            orderTotal = order.Total();

            store.Save(orderStream);

            orderProcessStream.AddEvent(orderProcess.Fulfill());

            store.Save(orderProcessStream);

            order = new Order(OrderId);
            orderStream = store.Get(OrderId);
            orderStream.Apply(order);

            Assert.AreEqual(orderTotal, order.Total(),
                "The total of the second re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

            orderProcess = new OrderProcess(OrderProcessId);
            orderProcessStream = store.Get(OrderProcessId);
            orderProcessStream.Apply(orderProcess);

            Assert.IsFalse(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled),
                "Should not be able to change status to 'Fulfilled'");

            var projection = new Projection("recall-fixture");
            var handler = new OrderHandler();

            projection.AddEventHandler(handler);

            processor.AddProjection(projection);

            handler.Start(handlerTimeoutSeconds);

            processor.Start();

            while (!(handler.IsComplete || handler.HasTimedOut))
            {
                Thread.Sleep(250);
            }

            Assert.IsFalse(handler.HasTimedOut, "The handler has timed out.  Not all of the events have been processed by the projection.");

            store.Remove(OrderId);
            store.Remove(orderProcessStream.Id);

            orderStream = store.Get(OrderId);
            orderProcessStream = store.Get(OrderProcessId);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
        }

        [Test]
        public void Should_be_able_to_exercise_event_store_and_processing()
        {
            var containerBuilder = new ContainerBuilder();
            var registry = new AutofacComponentRegistry(containerBuilder);
            var configurator = new EventStoreConfigurator(registry);

            registry.Register<IProjectionRepository, MemoryProjectionRepository>();
            registry.Register<IPrimitiveEventRepository, MemoryPrimitiveEventRepository>();

            configurator.RegisterComponents(new EventStoreConfiguration());

            var resolver = new AutofacComponentResolver(containerBuilder.Build());

            ExerciseEventStore(EventStore.Create(resolver), EventProcessor.Create(resolver), 60);
        }
    }
}