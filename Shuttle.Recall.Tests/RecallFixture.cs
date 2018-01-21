using System;
using System.Threading;
using NUnit.Framework;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests
{
    public class RecallFixture
    {
        public static readonly Guid OrderId = new Guid("047FF6FB-FB57-4F63-8795-99F252EDA62F");
        public static readonly Guid OrderProcessId = new Guid("74937207-F430-4746-9F31-4E76EF2FA7E6");

        public static void ExcerciseKeyStore(IKeyStore store)
        {
            Guard.AgainstNull(store, nameof(store));

            var id = OrderId;

            var value = string.Concat("value=", id.ToString());
            var anotherValue = string.Concat("anotherValue=", id.ToString());

            store.Add(id, value);

            Assert.Throws<Exception>(() => store.Add(id, value),
                $"Should not be able to add duplicate key / id = {id} / key = '{value}'");

            var idGet = store.Get(value);

            Assert.IsNotNull(idGet,
                $"Should be able to retrieve the id of the associated key / id = {id} / key = '{value}'");
            Assert.AreEqual(id, idGet,
                $"Should be able to retrieve the correct id of the associated key / id = {id} / key = '{value}' / id retrieved = {idGet}");

            idGet = store.Get(anotherValue);

            Assert.IsNull(idGet, $"Should not be able to get id of non-existent / id = {id} / key = '{anotherValue}'");

            store.Remove(id);

            idGet = store.Get(value);

            Assert.IsNull(idGet,
                $"Should be able to remove association using id (was not removed) / id = {id} / key = '{value}'");

            store.Add(id, value);
            store.Remove(value);

            idGet = store.Get(value);

            Assert.IsNull(idGet,
                $"Should be able to remove association using key (was not removed) / id = {id} / key = '{value}'");
        }

        public static void ExerciseStorage(IEventStore store)
        {
            Guard.AgainstNull(store, nameof(store));

            var order = new Order(OrderId);
            var orderProcess = new OrderProcess(OrderProcessId);

            var orderStream = store.CreateEventStream(OrderId);
            var orderProcessStream = store.CreateEventStream(OrderProcessId);

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

            store.Remove(OrderId);
            store.Remove(orderProcessStream.Id);

            orderStream = store.Get(OrderId);
            orderProcessStream = store.Get(OrderProcessId);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
        }

        public static void ExerciseEventProcessing(IEventStore store, IEventProcessor processor, int handlerTimeoutSeconds = 5)
        {
            Guard.AgainstNull(store, nameof(store));
            Guard.AgainstNull(processor, nameof(processor));

            var order = new Order(OrderId);
            var orderProcess = new OrderProcess(OrderProcessId);

            var orderStream = store.CreateEventStream(OrderId);
            var orderProcessStream = store.CreateEventStream(OrderProcessId);

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
    }
}