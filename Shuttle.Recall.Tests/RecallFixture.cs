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

    protected IEnumerable<Guid> AggregateIds = [OrderAId, OrderBId, OrderProcessId];

    /// <summary>
    ///     Event processing where 4 `ItemAdded` events are processed by the `OrderHandler` projection.
    /// </summary>
    public async Task ExerciseEventProcessingAsync(FixtureConfiguration fixtureConfiguration)
    {
        var handler = new OrderHandler();

        var serviceProvider = Guard.AgainstNull(fixtureConfiguration).Services
            .ConfigureLogging(nameof(ExerciseEventProcessingAsync))
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture").AddEventHandler(handler);

                builder.SuppressEventProcessorHostedService();

                fixtureConfiguration.EventStoreBuilderCallback?.Invoke(builder);
            })
            .BuildServiceProvider();

        fixtureConfiguration.ServiceProviderCallback?.Invoke(serviceProvider);

        await (fixtureConfiguration.RemoveIdsCallback?.Invoke(serviceProvider, AggregateIds) ?? Task.CompletedTask);

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

        handler.Start(fixtureConfiguration.HandlerTimeout);

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
    public async Task ExerciseEventProcessingWithDelayAsync(FixtureConfiguration fixtureConfiguration)
    {
        var processedEventCount = 0;

        var serviceProvider = Guard.AgainstNull(fixtureConfiguration).Services
            .ConfigureLogging(nameof(ExerciseEventProcessingWithDelayAsync))
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

                fixtureConfiguration.EventStoreBuilderCallback?.Invoke(builder);
            })
            .BuildServiceProvider();

        fixtureConfiguration.ServiceProviderCallback?.Invoke(serviceProvider);

        await (fixtureConfiguration.RemoveIdsCallback?.Invoke(serviceProvider, AggregateIds) ?? Task.CompletedTask);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderBId).ConfigureAwait(false);

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(1, 1);

        await semaphore.WaitAsync().ConfigureAwait(false);

        tasks.Add(Task.Run(async () =>
        {
            async Task Func()
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
            }

            if (fixtureConfiguration.EventStreamTaskCallback == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskCallback.Invoke(serviceProvider, Func);
            }
        }));

        await semaphore.WaitAsync().ConfigureAwait(false);

        tasks.Add(Task.Run(async () =>
        {
            async Task Func()
            {
                semaphore.Release();

                var order = new Order(OrderBId);
                var orderStream = await eventStore.GetAsync(OrderBId).ConfigureAwait(false);

                orderStream.Add(order.AddItem("item-1", 1, 100));
                orderStream.Add(order.AddItem("item-2", 2, 200));

                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

                await Task.Delay(2000);
            }

            if (fixtureConfiguration.EventStreamTaskCallback == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskCallback.Invoke(serviceProvider, Func);
            }
        }));

        tasks.Add(Task.Run(async () =>
        {
            async Task Func()
            {
                var order = new Order(OrderAId);
                var orderStream = await eventStore.GetAsync(OrderAId).ConfigureAwait(false);

                orderStream.Add(order.AddItem("item-3", 3, 300));
                orderStream.Add(order.AddItem("item-4", 4, 400));

                await eventStore.SaveAsync(orderStream).ConfigureAwait(false);
            }

            if (fixtureConfiguration.EventStreamTaskCallback == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskCallback.Invoke(serviceProvider, Func);
            }
        }));

        var processor = serviceProvider.GetRequiredService<IEventProcessor>();

        var timeout = DateTime.Now.Add(fixtureConfiguration.HandlerTimeout);
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
    public async Task ExerciseEventProcessingWithFailureAsync(FixtureConfiguration fixtureConfiguration)
    {
        var handler = new OrderHandler();

        var serviceProvider = Guard.AgainstNull(fixtureConfiguration.Services)
            .ConfigureLogging(nameof(ExerciseEventProcessingWithFailureAsync))
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture").AddEventHandler(handler);

                builder.SuppressEventProcessorHostedService();

                fixtureConfiguration.EventStoreBuilderCallback?.Invoke(builder);
            })
            .AddSingleton<IHostedService, FailureFixtureHostedService>()
            .BuildServiceProvider();

        fixtureConfiguration.ServiceProviderCallback?.Invoke(serviceProvider);

        await (fixtureConfiguration.RemoveIdsCallback?.Invoke(serviceProvider, AggregateIds) ?? Task.CompletedTask);

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

        handler.Start(fixtureConfiguration.HandlerTimeout);

        await processor.StartAsync().ConfigureAwait(false);

        while (!(handler.IsComplete || handler.HasTimedOut))
        {
            Thread.Sleep(250);
        }

        await processor.StopAsync().ConfigureAwait(false);

        Assert.That(handler.HasTimedOut, Is.False, "The handler has timed out.  Not all of the events have been processed by the projection.");

        await eventStore.RemoveAsync(OrderAId).ConfigureAwait(false);
    }

    public async Task ExerciseStorageAsync(FixtureConfiguration fixtureConfiguration)
    {
        Guard.AgainstNull(fixtureConfiguration).Services
            .ConfigureLogging(nameof(ExerciseStorageAsync))
            .AddEventStore(builder =>
            {
                fixtureConfiguration.EventStoreBuilderCallback?.Invoke(builder);

                builder.SuppressEventProcessorHostedService();
            });

        var serviceProvider = fixtureConfiguration.Services.BuildServiceProvider();

        fixtureConfiguration.ServiceProviderCallback?.Invoke(serviceProvider);

        await (fixtureConfiguration.RemoveIdsCallback?.Invoke(serviceProvider, AggregateIds) ?? Task.CompletedTask);

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