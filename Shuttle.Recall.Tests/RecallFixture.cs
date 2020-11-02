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
                "The total of the second re-constituted order does not equal the expected amount of '{0}'.",
                orderTotal);

            orderProcess = new OrderProcess(OrderProcessId);
            orderProcessStream = store.Get(OrderProcessId);
            orderProcessStream.Apply(orderProcess);

            Assert.IsFalse(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled),
                "Should not be able to change status to 'Fulfilled'");
        }

        public static void ExerciseEventProcessing(IEventProcessor processor, int handlerTimeoutSeconds = 5)
        {
            Guard.AgainstNull(processor, nameof(processor));

            var handler = new OrderHandler();

            var projection = processor.AddProjection("recall-fixture");

            projection.AddEventHandler(handler);

            handler.Start(handlerTimeoutSeconds);

            processor.Start();

            while (!(handler.IsComplete || handler.HasTimedOut))
            {
                Thread.Sleep(250);
            }

            processor.Stop();

            Assert.IsFalse(handler.HasTimedOut,
                "The handler has timed out.  Not all of the events have been processed by the projection.");
        }

        public static void ExerciseStorageRemoval(IEventStore store)
        {
            Guard.AgainstNull(store, nameof(store));

            store.Remove(OrderId);
            store.Remove(OrderProcessId);

            var orderStream = store.Get(OrderId);
            var orderProcessStream = store.Get(OrderProcessId);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
        }
    }
}