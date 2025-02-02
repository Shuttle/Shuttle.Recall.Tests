using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class FixtureConfiguration
{
    public IServiceCollection Services { get; }
    public Action<EventStoreBuilder>? AddEventStore { get; private set; }
    public Func<IServiceProvider, Task>? StartingAsync { get; private set; }
    public TimeSpan HandlerTimeout { get; private set; } = TimeSpan.FromSeconds(5);
    public Func<IServiceProvider, Func<Task>, Task>? EventStreamTaskAsync { get; set; }
    public Func<IEventHandlerContext<ItemAdded>, Task>? ItemAddedAsync { get; set; }
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

    public FixtureConfiguration WithStarting(Func<IServiceProvider, Task> starting)
    {
        StartingAsync = Guard.AgainstNull(starting);
        return this;
    }

    public FixtureConfiguration WithHandlerTimeout(TimeSpan handlerTimeout)
    {
        HandlerTimeout = handlerTimeout;
        return this;
    }

    public FixtureConfiguration WithEventStreamTask(Func<IServiceProvider, Func<Task>, Task> eventStreamTask)
    {
        EventStreamTaskAsync = Guard.AgainstNull(eventStreamTask);
        return this;
    }

    public FixtureConfiguration WithItemAdded(Func<IEventHandlerContext<ItemAdded>, Task> itemAdded)
    {
        ItemAddedAsync = Guard.AgainstNull(itemAdded);
        return this;
    }
}