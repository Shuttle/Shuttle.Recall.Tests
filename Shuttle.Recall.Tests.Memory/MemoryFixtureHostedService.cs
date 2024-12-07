using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.Tests.Memory;

internal class MemoryFixtureHostedService : IHostedService
{
    private readonly IPipelineFactory _pipelineFactory;
    private readonly Type _eventProcessorStartupPipelineType = typeof(EventProcessorStartupPipeline);

    public MemoryFixtureHostedService(IPipelineFactory pipelineFactory)
    {
        _pipelineFactory = Guard.AgainstNull(pipelineFactory);
        
        _pipelineFactory.PipelineCreated += OnPipelineCreated;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _pipelineFactory.PipelineCreated -= OnPipelineCreated;

        await Task.CompletedTask;
    }

    private void OnPipelineCreated(object? sender, PipelineEventArgs e)
    {
        var pipelineType = e.Pipeline.GetType();

        if (pipelineType == _eventProcessorStartupPipelineType)
        {
            e.Pipeline.AddObserver<MemoryFixtureStartupObserver>();
        }
    }
}