using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;
using Shuttle.Recall.Tests.Memory;

namespace Shuttle.Recall.Tests;

internal class FailureFixtureHostedService : IHostedService
{
    private readonly IPipelineFactory _pipelineFactory;
    private readonly Type _eventProcessingPipelineType = typeof(EventProcessingPipeline);
    private readonly FailureFixtureObserver _failureFixtureObserver;

    public FailureFixtureHostedService(IPipelineFactory pipelineFactory)
    {
        _pipelineFactory = Guard.AgainstNull(pipelineFactory);
        
        _pipelineFactory.PipelineCreated += OnPipelineCreated;
        _failureFixtureObserver = new(); // need a singleton for FixtureObserver._failedBefore
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

        if (pipelineType == _eventProcessingPipelineType)
        {
            e.Pipeline.AddObserver(_failureFixtureObserver);
        }
    }
}