﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.Tests;

public class RecallFixture
{
    public static readonly Guid OrderAId = new("047FF6FB-FB57-4F63-8795-99F252EDA62F");
    public static readonly Guid OrderBId = new("4587FA22-641B-4E79-A110-4350D237E7E2");
    public static readonly Guid OrderProcessId = new("74937207-F430-4746-9F31-4E76EF2FA7E6");
    private readonly Type _eventProcessingPipeline = typeof(EventProcessingPipeline);

    protected IEnumerable<Guid> KnownAggregateIds = [OrderAId, OrderBId, OrderProcessId];

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

                fixtureConfiguration.AddEventStore?.Invoke(builder);
            })
            .BuildServiceProvider();

        await (fixtureConfiguration.StartingAsync?.Invoke(serviceProvider) ?? Task.CompletedTask);

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
    }

    /// <summary>
    ///     PLEASE NOTE:
    ///     THIS FIXTURE WOULD NOT CLEAR ANY PREVIOUS RUNS.
    ///     Only run this in an environment where you intend clearing/managing the data manually.
    ///     Each iteration of the volume test will add 5 aggregates with 5 events each.
    /// </summary>
    public async Task ExerciseEventProcessingVolumeAsync(FixtureConfiguration fixtureConfiguration)
    {
        var processedEventCountA = 0;
        var processedEventCountB = 0;
        var processedEventCountC = 0;
        var projectionAggregates = new Dictionary<string, Dictionary<Guid, List<VolumeItem>>>();
        var semaphore = new SemaphoreSlim(1, 1);

        async Task AddProcessedItem(string projectionName, IEventHandlerContext<ItemAdded> context)
        {
            if (!projectionAggregates.ContainsKey(projectionName))
            {
                projectionAggregates.Add(projectionName, new());
            }

            if (!projectionAggregates[projectionName].ContainsKey(context.PrimitiveEvent.Id))
            {
                projectionAggregates[projectionName].Add(context.PrimitiveEvent.Id, new());
            }

            projectionAggregates[projectionName][context.PrimitiveEvent.Id].Add(new(context.PrimitiveEvent, context.Event));
        }

        var serviceProvider = Guard.AgainstNull(fixtureConfiguration).Services
            .ConfigureLogging(nameof(ExerciseEventProcessingVolumeAsync))
            .AddTransient<OrderHandler>()
            .AddEventStore(builder =>
            {
                builder.AddProjection("recall-fixture-a").AddEventHandler(async (ILogger<RecallFixture> logger, IEventHandlerContext<ItemAdded> context) =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        processedEventCountA++;

                        await AddProcessedItem("recall-fixture-a", context);

                        logger.LogDebug($"[recall-fixture-a] : event count = {processedEventCountA} / aggregate id = '{context.PrimitiveEvent.Id}' / product = '{context.Event.Product}' / sequence number = {context.PrimitiveEvent.SequenceNumber}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await (fixtureConfiguration.ItemAddedAsync?.Invoke(context) ?? Task.CompletedTask);
                });

                builder.AddProjection("recall-fixture-b").AddEventHandler(async (ILogger<RecallFixture> logger, IEventHandlerContext<ItemAdded> context) =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        processedEventCountB++;

                        await AddProcessedItem("recall-fixture-b", context);

                        logger.LogDebug($"[recall-fixture-b] : event count = {processedEventCountB} / aggregate id = '{context.PrimitiveEvent.Id}' / product = '{context.Event.Product}' / sequence number = {context.PrimitiveEvent.SequenceNumber}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await (fixtureConfiguration.ItemAddedAsync?.Invoke(context) ?? Task.CompletedTask);
                });

                builder.AddProjection("recall-fixture-c").AddEventHandler(async (ILogger<RecallFixture> logger, IEventHandlerContext<ItemAdded> context) =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        processedEventCountC++;

                        await AddProcessedItem("recall-fixture-c", context);

                        logger.LogDebug($"[recall-fixture-c] : event count = {processedEventCountC} / aggregate id = '{context.PrimitiveEvent.Id}' / product = '{context.Event.Product}' / sequence number = {context.PrimitiveEvent.SequenceNumber}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    await (fixtureConfiguration.ItemAddedAsync?.Invoke(context) ?? Task.CompletedTask);
                });

                builder.SuppressEventProcessorHostedService();

                builder.Options.DurationToSleepWhenIdle = [TimeSpan.FromMilliseconds(25)];

                fixtureConfiguration.AddEventStore?.Invoke(builder);
            })
            .BuildServiceProvider();

        await (fixtureConfiguration.StartingAsync?.Invoke(serviceProvider) ?? Task.CompletedTask);

        var pipelineFactory = serviceProvider.GetRequiredService<IPipelineFactory>();

        var idleProcessorThreads = new Dictionary<int, bool>();

        pipelineFactory.PipelineCreated += (_, pipelineEventArgs) =>
        {
            var pipelineType = pipelineEventArgs.Pipeline.GetType();

            if (pipelineType == _eventProcessingPipeline)
            {
                pipelineEventArgs.Pipeline.AddObserver(async (IPipelineContext<OnAbortPipeline> pipelineContext) =>
                {
                    var processorThreadManagedThreadId = pipelineContext.Pipeline.State.GetProcessorThreadManagedThreadId();

                    await semaphore.WaitAsync();

                    try
                    {
                        if (!idleProcessorThreads.TryAdd(processorThreadManagedThreadId, true))
                        {
                            idleProcessorThreads[processorThreadManagedThreadId] = true;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                pipelineEventArgs.Pipeline.AddObserver(async (IPipelineContext<OnAfterGetProjectionEvent> pipelineContext) =>
                {
                    var processorThreadManagedThreadId = pipelineContext.Pipeline.State.GetProcessorThreadManagedThreadId();

                    await semaphore.WaitAsync();

                    try
                    {
                        if (!idleProcessorThreads.TryAdd(processorThreadManagedThreadId, false))
                        {
                            idleProcessorThreads[processorThreadManagedThreadId] = false;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            }
        };

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var processor = serviceProvider.GetRequiredService<IEventProcessor>();

        await processor.StartAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        var random = new Random();

        int GetDelay()
        {
            return random.Next(0, 100) < 25 ? random.Next(20, 100) : 0;
        }

        var logger = serviceProvider.GetLogger<RecallFixture>();

        for (var iteration = 0; iteration < fixtureConfiguration.VolumeIterationCount; iteration++)
        {
            var tasks = new List<Task>();

            for (var aggregate = 0; aggregate < 5; aggregate++)
            {
                var id = Guid.NewGuid();

                logger.LogDebug($"[aggregate/iteration] : {aggregate} / {iteration}");

                tasks.Add(Task.Run(async () =>
                {
                    async Task Func()
                    {
                        var order = new Order(id);
                        var orderStream = await eventStore.GetAsync(id).ConfigureAwait(false);

                        orderStream.Add(order.AddItem("item-1", 1, 100));
                        orderStream.Add(order.AddItem("item-2", 2, 200));
                        orderStream.Add(order.AddItem("item-3", 3, 300));
                        orderStream.Add(order.AddItem("item-4", 4, 400));
                        orderStream.Add(order.AddItem("item-5", 5, 500));

                        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

                        await Task.Delay(GetDelay());
                    }

                    if (fixtureConfiguration.EventStreamTaskAsync == null)
                    {
                        await Func();
                    }
                    else
                    {
                        await fixtureConfiguration.EventStreamTaskAsync.Invoke(serviceProvider, Func);
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        var timeout = DateTime.Now.Add(fixtureConfiguration.HandlerTimeout);
        var hasTimedOut = false;

        var expectedProcessedEventCount = fixtureConfiguration.VolumeIterationCount * 25 * 3;

        while (processedEventCountA + processedEventCountB + processedEventCountC < expectedProcessedEventCount && !hasTimedOut)
        {
            Thread.Sleep(250);

            hasTimedOut = DateTime.Now > timeout;
        }

        // Wait until all processor threads are idle.
        while (!idleProcessorThreads.All(item => item.Value) && !hasTimedOut)
        {
            Thread.Sleep(250);

            hasTimedOut = DateTime.Now > timeout;
        }

        await processor.StopAsync().ConfigureAwait(false);

        Assert.That(hasTimedOut, Is.False, $"The fixture has timed out.  Processed {processedEventCountA + processedEventCountB + processedEventCountC} events out of expected {expectedProcessedEventCount} events.");

        // Check that all aggregates were processed in order.
        foreach (var projectionAggregate in projectionAggregates)
        {
            foreach (var aggregate in projectionAggregate.Value)
            {
                Assert.That(aggregate.Value.Select(item => item.PrimitiveEvent.SequenceNumber).ToList(), Is.Ordered, $"Projection '{projectionAggregate.Key}' has aggregate '{aggregate.Key}' where the sequence numbers are not ordered.");
            }
        }
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

                fixtureConfiguration.AddEventStore?.Invoke(builder);
            })
            .BuildServiceProvider();

        await (fixtureConfiguration.StartingAsync?.Invoke(serviceProvider) ?? Task.CompletedTask);

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

            if (fixtureConfiguration.EventStreamTaskAsync == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskAsync.Invoke(serviceProvider, Func);
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

            if (fixtureConfiguration.EventStreamTaskAsync == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskAsync.Invoke(serviceProvider, Func);
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

            if (fixtureConfiguration.EventStreamTaskAsync == null)
            {
                await Func();
            }
            else
            {
                await fixtureConfiguration.EventStreamTaskAsync.Invoke(serviceProvider, Func);
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

                fixtureConfiguration.AddEventStore?.Invoke(builder);
            })
            .AddSingleton<IHostedService, FailureFixtureHostedService>()
            .BuildServiceProvider();

        await (fixtureConfiguration.StartingAsync?.Invoke(serviceProvider) ?? Task.CompletedTask);

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
    }

    public async Task ExerciseStorageAsync(FixtureConfiguration fixtureConfiguration)
    {
        Guard.AgainstNull(fixtureConfiguration).Services
            .ConfigureLogging(nameof(ExerciseStorageAsync))
            .AddEventStore(builder =>
            {
                fixtureConfiguration.AddEventStore?.Invoke(builder);

                builder.SuppressEventProcessorHostedService();
            });

        var serviceProvider = fixtureConfiguration.Services.BuildServiceProvider();

        await (fixtureConfiguration.StartingAsync?.Invoke(serviceProvider) ?? Task.CompletedTask);

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

    public class VolumeItem
    {
        public VolumeItem(PrimitiveEvent primitiveEvent, ItemAdded itemAdded)
        {
            PrimitiveEvent = primitiveEvent;
            ItemAdded = itemAdded;
        }

        public ItemAdded ItemAdded { get; }
        public PrimitiveEvent PrimitiveEvent { get; }
    }
}