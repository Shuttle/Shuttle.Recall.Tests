using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Shuttle.Core.Contract;
using Shuttle.Recall.Logging;

namespace Shuttle.Recall.Tests;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureLogging(this IServiceCollection services, string test)
    {
        Guard.AgainstNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider>(new FixtureFileLoggerProvider(Guard.AgainstNullOrEmptyString(test))));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ConsoleLoggerProvider>());

        services.AddEventStoreLogging(builder =>
        {
            builder.Options.Threading = true;
        });

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        return services;
    }
}