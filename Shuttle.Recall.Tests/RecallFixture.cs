﻿using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests
{
    public class RecallFixture
    {
        public static readonly Guid OrderId = new Guid("047FF6FB-FB57-4F63-8795-99F252EDA62F");
        public static readonly Guid OrderProcessId = new Guid("74937207-F430-4746-9F31-4E76EF2FA7E6");

        public void ExerciseEventProcessing(IEventStore eventStore, IEventProcessor processor, int handlerTimeoutSeconds = 5)
        {
            ExerciseEventProcessingAsync(eventStore, processor, handlerTimeoutSeconds, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseEventProcessingAsync(IEventStore eventStore, IEventProcessor processor, int handlerTimeoutSeconds = 5)
        {
            await ExerciseEventProcessingAsync(eventStore, processor, handlerTimeoutSeconds, false).ConfigureAwait(false);
        }

        private async Task ExerciseEventProcessingAsync(IEventStore eventStore, IEventProcessor processor, int handlerTimeoutSeconds, bool sync)
        {
            Guard.AgainstNull(eventStore, nameof(eventStore));
            Guard.AgainstNull(processor, nameof(processor));

            if (sync)
            {
                ExerciseStorage(eventStore);
            }
            else
            {
                await ExerciseStorageAsync(eventStore).ConfigureAwait(false);
            }

            var handler = new OrderHandler();

            var projection = sync ? processor.AddProjection("recall-fixture") : await processor.AddProjectionAsync("recall-fixture").ConfigureAwait(false);

            if (sync)
            {
                projection.AddEventHandler(handler);
            }
            else
            {
                await projection.AddEventHandlerAsync(handler).ConfigureAwait(false);
            }

            handler.Start(handlerTimeoutSeconds);

            if (sync)
            {
                processor.Start();
            }
            else
            {
                await processor.StartAsync().ConfigureAwait(false);
            }

            while (!(handler.IsComplete || handler.HasTimedOut))
            {
                Thread.Sleep(250);
            }

            if (sync)
            {
                processor.Stop();
            }
            else
            {
                await processor.StopAsync().ConfigureAwait(false);
            }

            Assert.IsFalse(handler.HasTimedOut, "The handler has timed out.  Not all of the events have been processed by the projection.");

            if (sync)
            {
                ExerciseStorageRemoval(eventStore);
            }
            else
            {
                await ExerciseStorageRemovalAsync(eventStore).ConfigureAwait(false);
            }
        }

        public void ExerciseStorage(IEventStore eventStore)
        {
            ExerciseStorageAsync(eventStore, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseStorageAsync(IEventStore eventStore)
        {
            await ExerciseStorageAsync(eventStore, false).ConfigureAwait(false);
        }

        private async Task ExerciseStorageAsync(IEventStore eventStore, bool sync)
        {
            Guard.AgainstNull(eventStore, nameof(eventStore));

            var order = new Order(OrderId);
            var orderProcess = new OrderProcess(OrderProcessId);

            var orderStream = sync ? eventStore.Get(OrderId) : await eventStore.GetAsync(OrderId).ConfigureAwait(false);
            var orderProcessStream = sync ? eventStore.Get(OrderProcessId) : await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

            orderStream.AddEvent(order.AddItem("t-shirt", 5, 125));
            orderStream.AddEvent(order.AddItem("baked beans", 2, 4.55));
            orderStream.AddEvent(order.AddItem("20L white glossy enamel paint", 1, 700));

            var orderTotal = order.Total();

            if (sync)
            {
                eventStore.Save(orderStream);
            }
            else
            {
                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
            }

            orderProcessStream.AddEvent(orderProcess.StartPicking());

            if (sync)
            {
                eventStore.Save(orderProcessStream);
            }
            else
            {
                await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);
            }

            order = new Order(OrderId);
            orderStream = sync ? eventStore.Get(OrderId) : await eventStore.GetAsync(OrderId).ConfigureAwait(false);

            orderStream.Apply(order);

            Assert.AreEqual(orderTotal, order.Total(), "The total of the first re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

            orderStream.AddSnapshot(order.GetSnapshotEvent());

            if (sync)
            {
                eventStore.Save(orderStream);
            }
            else
            {
                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
            }

            orderProcess = new OrderProcess(OrderProcessId);
            orderProcessStream = sync ? eventStore.Get(OrderProcessId) : await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);
            orderProcessStream.Apply(orderProcess);

            Assert.IsTrue(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), "Should be able to change status to 'Fulfilled'");

            orderStream.AddEvent(order.AddItem("4kg bag of potatoes", 5, 15.35));

            orderTotal = order.Total();

            if (sync)
            {
                eventStore.Save(orderStream);
            }
            else
            {
                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
            }

            orderProcessStream.AddEvent(orderProcess.Fulfill());

            if (sync)
            {
                eventStore.Save(orderProcessStream);
            }
            else
            {
                await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);
            }

            order = new Order(OrderId);
            orderStream = sync ? eventStore.Get(OrderId) : await eventStore.GetAsync(OrderId).ConfigureAwait(false);
            orderStream.Apply(order);

            Assert.AreEqual(orderTotal, order.Total(), "The total of the second re-constituted order does not equal the expected amount of '{0}'.", orderTotal);

            orderProcess = new OrderProcess(OrderProcessId);
            orderProcessStream = sync ? eventStore.Get(OrderProcessId) : await eventStore.GetAsync(OrderProcessId);
            orderProcessStream.Apply(orderProcess);

            Assert.IsFalse(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), "Should not be able to change status to 'Fulfilled'");
        }

        public void ExerciseStorageRemoval(IEventStore store)
        {
            ExerciseStorageRemovalAsync(store, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseStorageRemovalAsync(IEventStore store)
        {
            await ExerciseStorageRemovalAsync(store, false).ConfigureAwait(false);
        }

        private async Task ExerciseStorageRemovalAsync(IEventStore store, bool sync)
        {
            Guard.AgainstNull(store, nameof(store));

            if (sync)
            {
                store.Remove(OrderId);
                store.Remove(OrderProcessId);
            }
            else
            {
                await store.RemoveAsync(OrderId).ConfigureAwait(false);
                await store.RemoveAsync(OrderProcessId).ConfigureAwait(false);
            }

            var orderStream = sync ? store.Get(OrderId) : await store.GetAsync(OrderId).ConfigureAwait(false);
            var orderProcessStream = sync ? store.Get(OrderProcessId) : await store.GetAsync(OrderProcessId).ConfigureAwait(false);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
        }
    }
}