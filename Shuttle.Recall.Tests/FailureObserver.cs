using System;
using System.Threading.Tasks;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.Tests.Memory;

internal class FailureFixtureObserver : IPipelineObserver<OnAfterHandleEvent>
{
    private bool _failedBefore;

    public async Task ExecuteAsync(IPipelineContext<OnAfterHandleEvent> pipelineContext)
    {
        var itemAdded = pipelineContext.Pipeline.State.GetDomainEvent().Event as ItemAdded;

        if (itemAdded == null)
        {
            return;
        }

        if (itemAdded.Product.Equals("item-3") && !_failedBefore)
        {
            _failedBefore = true;

            var message = $"[{nameof(FailureFixtureObserver)}] : One-time failure of 'item-3'.";

            Console.WriteLine(message);

            throw new ApplicationException(message);
        }

        await Task.CompletedTask;
    }
}