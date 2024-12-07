using System;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;
using Shuttle.Core.Threading;
using Shuttle.Recall.Tests.Memory.Fakes;

namespace Shuttle.Recall.Tests.Memory;

public class MemoryFixtureStartupObserver : IPipelineObserver<OnAfterStartThreadPools>
{
    private readonly IProjectionService _projectionService;

    public MemoryFixtureStartupObserver(IProjectionService projectionService)
    {
        _projectionService = Guard.AgainstNull(projectionService);
    }

    public async Task ExecuteAsync(IPipelineContext<OnAfterStartThreadPools> pipelineContext)
    {
        if (_projectionService is not MemoryProjectionService service)
        {
            throw new InvalidOperationException($"The projection event service must be of type '{typeof(MemoryProjectionService).FullName}'; instead found type '{_projectionService.GetType().FullName}'.");
        }

        await service.StartupAsync(Guard.AgainstNull(Guard.AgainstNull(pipelineContext).Pipeline.State.Get<IProcessorThreadPool>("EventProcessorThreadPool")));
    }
}