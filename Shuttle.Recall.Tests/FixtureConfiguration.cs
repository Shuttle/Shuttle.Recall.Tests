using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests;

public class FixtureConfiguration
{
    public IServiceCollection Services { get; }
    public Action<EventStoreBuilder>? EventStoreBuilderCallback { get; private set; }
    public Action<IServiceProvider>? ServiceProviderCallback { get; private set; }
    public Func<IEnumerable<Guid>, Task>? RemoveIdsCallback { get; private set; }
    public TimeSpan HandlerTimeout { get; private set; } = TimeSpan.FromSeconds(5);
    public Func<Func<Task>, Task>? EventStreamTaskCallback { get; set; }

    public FixtureConfiguration(IServiceCollection services)
    {
        Services = Guard.AgainstNull(services);
    }

    public FixtureConfiguration WithEventStoreBuilderCallback(Action<EventStoreBuilder> eventStoreBuilderCallback)
    {
        EventStoreBuilderCallback = Guard.AgainstNull(eventStoreBuilderCallback);
        return this;
    }

    public FixtureConfiguration WithServiceProviderCallback(Action<IServiceProvider> serviceProviderCallback)
    {
        ServiceProviderCallback = Guard.AgainstNull(serviceProviderCallback);
        return this;
    }

    public FixtureConfiguration WithRemoveIdsCallback(Func<IEnumerable<Guid>, Task> removeIdsCallback)
    {
        RemoveIdsCallback = Guard.AgainstNull(removeIdsCallback);
        return this;
    }

    public FixtureConfiguration WithHandlerTimeout(TimeSpan handlerTimeout)
    {
        HandlerTimeout = handlerTimeout;
        return this;
    }

    public FixtureConfiguration WithEventStreamTaskCallback(Func<Func<Task>, Task> eventStreamTaskCallback)
    {
        EventStreamTaskCallback = Guard.AgainstNull(eventStreamTaskCallback);
        return this;
    }
}