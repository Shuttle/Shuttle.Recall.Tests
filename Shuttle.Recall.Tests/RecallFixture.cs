using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests
{
    public class RecallFixture
    {
        public static readonly Guid OrderId = new Guid("047FF6FB-FB57-4F63-8795-99F252EDA62F");
        public static readonly Guid OrderProcessId = new Guid("74937207-F430-4746-9F31-4E76EF2FA7E6");

        public void ExerciseEventProcessing(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
        {
            ExerciseEventProcessingAsync(services, serviceProviderCallback, handlerTimeoutSeconds, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseEventProcessingAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
        {
            await ExerciseEventProcessingAsync(services, serviceProviderCallback, handlerTimeoutSeconds, false).ConfigureAwait(false);
        }

        private async Task ExerciseEventProcessingAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback, int handlerTimeoutSeconds, bool sync)
        {
            Guard.AgainstNull(services, nameof(services));

            services.ConfigureLogging(nameof(ExerciseEventProcessingAsync));

            var serviceProvider = services.BuildServiceProvider();

            serviceProviderCallback?.Invoke(serviceProvider);

            if (sync)
            {
                serviceProvider.StartHostedServices();
            }
            else
            {
                await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);
            }

            var processor = serviceProvider.GetRequiredService<IEventProcessor>();

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
        }

        public void ExerciseStorage(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null)
        {
            ExerciseStorageAsync(services, serviceProviderCallback, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseStorageAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null)
        {
            await ExerciseStorageAsync(services, serviceProviderCallback, false).ConfigureAwait(false);
        }

        private async Task ExerciseStorageAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback, bool sync)
        {
            Guard.AgainstNull(services, nameof(services));

            services.ConfigureLogging(nameof(ExerciseStorageAsync));

            var serviceProvider = services.BuildServiceProvider();

            serviceProviderCallback?.Invoke(serviceProvider);

            if(sync)
            {
                serviceProvider.StartHostedServices();
            }
            else
            {
                await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);
            }

            var eventStore = serviceProvider.GetRequiredService<IEventStore>();

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

        public void ExerciseStorageRemoval(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null)
        {
            ExerciseStorageRemovalAsync(services, serviceProviderCallback, true).GetAwaiter().GetResult();
        }

        public async Task ExerciseStorageRemovalAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback = null)
        {
            await ExerciseStorageRemovalAsync(services, serviceProviderCallback, false).ConfigureAwait(false);
        }

        private async Task ExerciseStorageRemovalAsync(IServiceCollection services, Action<IServiceProvider> serviceProviderCallback, bool sync)
        {
            Guard.AgainstNull(services, nameof(services));

            services.ConfigureLogging(nameof(ExerciseStorageRemovalAsync));

            var serviceProvider = services.BuildServiceProvider();

            serviceProviderCallback?.Invoke(serviceProvider);
            
            if(sync)
            {
                serviceProvider.StartHostedServices();
            }
            else
            {
                await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);
            }

            var eventStore = serviceProvider.GetRequiredService<IEventStore>();

            if (sync)
            {
                eventStore.Remove(OrderId);
                eventStore.Remove(OrderProcessId);
            }
            else
            {
                await eventStore.RemoveAsync(OrderId).ConfigureAwait(false);
                await eventStore.RemoveAsync(OrderProcessId).ConfigureAwait(false);
            }

            var orderStream = sync ? eventStore.Get(OrderId) : await eventStore.GetAsync(OrderId).ConfigureAwait(false);
            var orderProcessStream = sync ? eventStore.Get(OrderProcessId) : await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

            Assert.IsTrue(orderStream.IsEmpty);
            Assert.IsTrue(orderProcessStream.IsEmpty);
        }
    }
}