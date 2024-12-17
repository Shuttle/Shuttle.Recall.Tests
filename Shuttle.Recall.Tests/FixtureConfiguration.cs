using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class FixtureConfiguration
{
    public IServiceCollection Services { get; }
    public Action<EventStoreBuilder>? AddEventStore { get; private set; }
    public Action<IServiceProvider>? ServiceProviderAvailable { get; private set; }
    public Func<IServiceProvider, IEnumerable<Guid>, Task>? RemoveIds { get; private set; }
    public TimeSpan HandlerTimeout { get; private set; } = TimeSpan.FromSeconds(5);
    public Func<IServiceProvider, Func<Task>, Task>? EventStreamTask { get; set; }
    public Func<IEventHandlerContext<ItemAdded>, Task>? ItemAdded { get; set; }
    public int VolumeIterationCount { get; set; } = 100;

    public FixtureConfiguration(IServiceCollection services)
    {
        Services = Guard.AgainstNull(services);
    }

    public FixtureConfiguration WithAddEventStore(Action<EventStoreBuilder> addEventStore)
    {
        AddEventStore = Guard.AgainstNull(addEventStore);
        return this;
    }

    public FixtureConfiguration WithServiceProviderAvailable(Action<IServiceProvider> serviceProviderAvailable)
    {
        ServiceProviderAvailable = Guard.AgainstNull(serviceProviderAvailable);
        return this;
    }

    public FixtureConfiguration WithRemoveIds(Func<IServiceProvider, IEnumerable<Guid>, Task> removeIds)
    {
        RemoveIds = Guard.AgainstNull(removeIds);
        return this;
    }

    public FixtureConfiguration WithHandlerTimeout(TimeSpan handlerTimeout)
    {
        HandlerTimeout = handlerTimeout;
        return this;
    }

    public FixtureConfiguration WithEventStreamTask(Func<IServiceProvider, Func<Task>, Task> eventStreamTask)
    {
        EventStreamTask = Guard.AgainstNull(eventStreamTask);
        return this;
    }

    public FixtureConfiguration WithItemAdded(Func<IEventHandlerContext<ItemAdded>, Task> itemAdded)
    {
        ItemAdded = Guard.AgainstNull(itemAdded);
        return this;
    }
}