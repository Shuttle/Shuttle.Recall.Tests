using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class RecallFixture
{
    public static readonly Guid OrderId = new("047FF6FB-FB57-4F63-8795-99F252EDA62F");
    public static readonly Guid OrderProcessId = new("74937207-F430-4746-9F31-4E76EF2FA7E6");

    public async Task ExerciseEventProcessingAsync(IServiceCollection services, Action<EventStoreBuilder>? eventStoreBuilderCallback = null, Action<IServiceProvider>? serviceProviderCallback = null, int handlerTimeoutSeconds = 5)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseEventProcessingAsync));

        var handler = new OrderHandler();

        services.AddEventStore(builder =>
        {
            builder.AddProjection("recall-fixture").AddEventHandler(handler);

            builder.SuppressEventProcessorHostedService();

            eventStoreBuilderCallback?.Invoke(builder);
        });

        services.AddTransient<OrderHandler>();

        var serviceProvider = services.BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        await eventStore.RemoveAsync(OrderId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderProcessId).ConfigureAwait(false);
        
        var order = new Order(OrderId);

        var orderStream = await eventStore.GetAsync(OrderId).ConfigureAwait(false);
        
        orderStream.Add(order.AddItem("t-shirt", 5, 125));
        orderStream.Add(order.AddItem("baked beans", 2, 4.55));
        orderStream.Add(order.AddItem("20L white glossy enamel paint", 1, 700));
        orderStream.Add(order.AddItem("4kg bag of potatoes", 5, 15.35));

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

        await eventStore.RemoveAsync(OrderId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderProcessId).ConfigureAwait(false);
    }

    public async Task ExerciseStorageAsync(IServiceCollection services, Action<IServiceProvider>? serviceProviderCallback = null)
    {
        Guard.AgainstNull(services).ConfigureLogging(nameof(ExerciseStorageAsync));

        services.AddEventStore();

        var serviceProvider = services.BuildServiceProvider();

        serviceProviderCallback?.Invoke(serviceProvider);

        await serviceProvider.StartHostedServicesAsync().ConfigureAwait(false);

        var eventStore = serviceProvider.GetRequiredService<IEventStore>();

        var order = new Order(OrderId);
        var orderProcess = new OrderProcess(OrderProcessId);

        var orderStream = await eventStore.GetAsync(OrderId).ConfigureAwait(false);
        var orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

        orderStream.Add(order.AddItem("t-shirt", 5, 125));
        orderStream.Add(order.AddItem("baked beans", 2, 4.55));
        orderStream.Add(order.AddItem("20L white glossy enamel paint", 1, 700));

        var orderTotal = order.Total();

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcessStream.Add(orderProcess.StartPicking());

        await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);

        order = new(OrderId);
        orderStream = await eventStore.GetAsync(OrderId).ConfigureAwait(false);

        orderStream.Apply(order);

        Assert.That(order.Total(), Is.EqualTo(orderTotal), $"The total of the first re-constituted order does not equal the expected amount of '{orderTotal}'.");

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcess = new(OrderProcessId);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);
        orderProcessStream.Apply(orderProcess);

        Assert.That(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), Is.True, "Should be able to change status to 'Fulfilled'");

        orderStream.Add(order.AddItem("4kg bag of potatoes", 5, 15.35));

        orderTotal = order.Total();

        await eventStore.SaveAsync(orderStream).ConfigureAwait(false);

        orderProcessStream.Add(orderProcess.Fulfill());

        await eventStore.SaveAsync(orderProcessStream).ConfigureAwait(false);

        order = new(OrderId);
        orderStream = await eventStore.GetAsync(OrderId).ConfigureAwait(false);
        orderStream.Apply(order);

        Assert.That(order.Total(), Is.EqualTo(orderTotal), $"The total of the second re-constituted order does not equal the expected amount of '{orderTotal}'.");

        orderProcess = new(OrderProcessId);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId);
        orderProcessStream.Apply(orderProcess);

        Assert.That(orderProcess.CanChangeStatusTo(OrderProcessStatus.Fulfilled), Is.False, "Should not be able to change status to 'Fulfilled'");

        await eventStore.RemoveAsync(OrderId).ConfigureAwait(false);
        await eventStore.RemoveAsync(OrderProcessId).ConfigureAwait(false);

        orderStream = await eventStore.GetAsync(OrderId).ConfigureAwait(false);
        orderProcessStream = await eventStore.GetAsync(OrderProcessId).ConfigureAwait(false);

        Assert.That(orderStream.IsEmpty, Is.True);
        Assert.That(orderProcessStream.IsEmpty, Is.True);
    }
}