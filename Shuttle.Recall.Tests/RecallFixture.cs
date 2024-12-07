using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class RecallFixture
{
    public static readonly Guid OrderAId = new("047FF6FB-FB57-4F63-8795-99F252EDA62F");
    public static readonly Guid OrderBId = new("4587FA22-641B-4E79-A110-4350D237E7E2");
    public static readonly Guid OrderProcessId = new("74937207-F430-4746-9F31-4E76EF2FA7E6");

    /// <summary>
    ///     Event processing where 4 `ItemAdded` events are processed by the `OrderHandler` projection.
    /// </summary>
    public async Task ExerciseEventProcessingAsync(IServiceCollection services, Action<EventStoreBuilder>? eventStoreBuilderCallback = null, Action<IServiceProvider>? serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseEventProcessingAsync));

        var handler = new OrderHandler();

        var serviceProvider = services
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture").AddEventHandler(handler);

                builder.SuppressEventProcessorHostedService();

                eventStoreBuilderCallback?.Invoke(builder);
            })
            .BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);

        var order = new Order(OrderAId);

        var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

        orderStream.Add(order.AddItem("item-1", 1, 100));
        orderStream.Add(order.AddItem("item-2", 2, 200));
        orderStream.Add(order.AddItem("item-3", 3, 300));
        orderStream.Add(order.AddItem("item-4", 4, 400));

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        var processor = serviceProvider.GetRequiredService<IEventProcessor>();

        handler.Start(handlerTimeoutSeconds);

        await processor.StartAsync().ConfigureAwait(false);

        while (!(handler.IsComplete || handler.HasTimedOut))
        {
            Thread.Sleep(250);
        }

        await processor.StopAsync().ConfigureAwait(false);

        Assert.That(handler.HasTimedOut, Is.False, "The handler has timed out.  Not all of the events have been processed by the projection.");

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Event processing where 2 `ItemAdded` events are added for the correlation id (CID-A) being tested.
    ///     These are followed by events being added to another correlation id (CID-B) but the transaction is delayed.
    ///     We then added 2 more `ItemAdded` events for the correlation id being tested (CID-A).
    ///     The projection processing should *NOT* process these last two events for CID-A until the transaction for CID-B has
    ///     been completed.
    ///     This would preserver the global sequence number tracking of the projection.
    /// </summary>
    public async Task ExerciseEventProcessingWithDelayAsync(IServiceCollection services, Action<EventStoreBuilder>? eventStoreBuilderCallback = null, Action<IServiceProvider>? serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseEventProcessingWithDelayAsync));

        var processedEventCount = 0;

        var serviceProvider = services
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture").AddEventHandler(async (IEventHandlerContext<ItemAdded> context) =>
                {
                    processedEventCount++;

                    Console.WriteLine($"[processed] : aggregate id = '{context.PrimitiveEvent.Id}' / product = '{context.Event.Product}' / sequence number = {context.PrimitiveEvent.SequenceNumber} / projection sequence number = {context.Projection.SequenceNumber}");

                    await Task.CompletedTask;
                });

                builder.SuppressEventProcessorHostedService();

                eventStoreBuilderCallback?.Invoke(builder);
            })
            .BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderBId).ConfigureAwait(false);

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(1, 1);

        await semaphore.WaitAsync().ConfigureAwait(false);

        tasks.Add(Task.Run(async () =>
        {
            try
            {
                var order = new Order(OrderAId);
                var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

                orderStream.Add(order.AddItem("item-1", 1, 100));
                orderStream.Add(order.AddItem("item-2", 2, 200));

                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }));

        await semaphore.WaitAsync().ConfigureAwait(false);

        tasks.Add(Task.Run(async () =>
        {
            semaphore.Release();

            var order = new Order(OrderBId);
            var orderStream = await eventStore.GetAsync(OrderBId).ConfigureAwait(false);

            orderStream.Add(order.AddItem("item-1", 1, 100));
            orderStream.Add(order.AddItem("item-2", 2, 200));

            await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

            await Task.Delay(2000);
        }));

        tasks.Add(Task.Run(async () =>
        {
            var order = new Order(OrderAId);
            var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

            orderStream.Add(order.AddItem("item-3", 3, 300));
            orderStream.Add(order.AddItem("item-4", 4, 400));

            await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
        }));

        var processor = serviceProvider.GetRequiredService<IEventProcessor>();

        var timeout = DateTime.Now.AddSeconds(handlerTimeoutSeconds);
        var hasTimedOut = false;

        await processor.StartAsync().ConfigureAwait(false);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        while (processedEventCount < 6 && !hasTimedOut)
        {
            Thread.Sleep(250);

            hasTimedOut = DateTime.Now > timeout;
        }

        await processor.StopAsync().ConfigureAwait(false);

        Assert.That(hasTimedOut, Is.False, "The fixture has timed out.  Not all of the events have been processed by the projection.");

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderBId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Event processing where 4 `ItemAdded` events are processed by the `OrderHandler` projection.
    ///     However, there is a transient error that occurs during the processing of the 3rd event.
    /// </summary>
    public async Task ExerciseEventProcessingWithFailureAsync(IServiceCollection services, Action<EventStoreBuilder>? eventStoreBuilderCallback = null, Action<IServiceProvider>? serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseEventProcessingWithFailureAsync));

        var handler = new OrderHandler();

        var serviceProvider = services
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture").AddEventHandler(handler);

                builder.SuppressEventProcessorHostedService();

                eventStoreBuilderCallback?.Invoke(builder);
            })
            .AddSingleton<IHostedService, FailureFixtureHostedService>()
            .BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);

        var order = new Order(OrderAId);

        var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

        orderStream.Add(order.AddItem("item-1", 1, 100));
        orderStream.Add(order.AddItem("item-2", 2, 200));
        orderStream.Add(order.AddItem("item-3", 3, 300));
        orderStream.Add(order.AddItem("item-4", 4, 400));

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        var processor = serviceProvider.GetRequiredService<IEventProcessor>();

        handler.Start(handlerTimeoutSeconds);

        await processor.StartAsync().ConfigureAwait(false);

        while (!(handler.IsComplete || handler.HasTimedOut))
        {
            Thread.Sleep(250);
        }

        await processor.StopAsync().ConfigureAwait(false);

        Assert.That(handler.HasTimedOut, Is.False, "The handler has timed out.  Not all of the events have been processed by the projection.");

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
    }

    public async Task ExerciseStorageAsync(IServiceCollection services, Action<IServiceProvider>? serviceProviderCallback = null)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseStorageAsync));

        services
            .AddEventStore(builder =>
            {
                builder.SuppressEventProcessorHostedService();
            });

        var serviceProvider = services.BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        var order = new Order(OrderAId);
        var orderProcess = new OrderProcess(OrderProcessId);

        var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);
        var orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

        orderStream.Add(order.AddItem("item-1", 1, 100));
        orderStream.Add(order.AddItem("item-2", 2, 200));
        orderStream.Add(order.AddItem("item-3", 3, 300));

        var orderTotal = order.Total();

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcessStream.Add(orderProcess.StartPicking());

        await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);

        order = new(OrderAId);
        orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

        orderStream.Apply(order);

        Assert.That(order.Total(), Is.EqualTo(orderTotal), $"The total of the first re-constituted order does not equal the expected amount of '{orderTotal}'.");

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcess = new(OrderProcessId);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);
        orderProcessStream.Apply(orderProcess);

        Assert.That(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), Is.True, "Should be able to change status to 'Fulfilled'");

        orderStream.Add(order.AddItem("item-4", 4, 400));

        orderTotal = order.Total();

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcessStream.Add(orderProcess.Fulfill());

        await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);

        order = new(OrderAId);
        orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);
        orderStream.Apply(order);

        Assert.That(order.Total(), Is.EqualTo(orderTotal), $"The total of the second re-constituted order does not equal the expected amount of '{orderTotal}'.");

        orderProcess = new(OrderProcessId);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId);
        orderProcessStream.Apply(orderProcess);

        Assert.That(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), Is.False, "Should not be able to change status to 'Fulfilled'");

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderProcessId).ConfigureAwait(false);

        orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

        Assert.That(orderStream.IsEmpty, Is.True);
        Assert.That(orderProcessStream.IsEmpty, Is.True);
    }
}