using System;
using System.Threading.Tasks;

namespace Shuttle.Recall.Tests;

public class OrderHandler : IEventHandler<ItemAdded>
{
    private int _count;
    private DateTime _timeOutDate = DateTime.MaxValue;
    public bool HasTimedOut => _timeOutDate < DateTime.Now;

    public bool IsComplete => _count == 4; // 4 x ItemAdded events from ExerciseStorageAsync

    public async Task ProcessEventAsync(IEventHandlerContext<ItemAdded> context)
    {
        _count++;

        await Task.CompletedTask;
    }

    public void Start(int handlerTimeoutSeconds)
    {
        _timeOutDate = DateTime.Now.AddSeconds(handlerTimeoutSeconds < 5 ? 5 : handlerTimeoutSeconds);
    }
}