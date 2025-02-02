using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shuttle.Recall.Tests;

public class OrderHandler : IEventHandler<ItemAdded>
{
    private readonly List<ItemAdded> _events = new();
    private DateTime _timeOutDate = DateTime.MaxValue;
    public bool HasTimedOut => _timeOutDate < DateTime.Now;

    public bool IsComplete => _events.Count == 4;

    public async Task ProcessEventAsync(IEventHandlerContext<ItemAdded> context)
    {
        if (_events.FirstOrDefault(item => item.Product.Equals(context.Event.Product, StringComparison.InvariantCultureIgnoreCase)) != null)
        {
            return;
        }

        _events.Add(context.Event);

        await Task.CompletedTask;
    }

    public void Start(TimeSpan handlerTimeout)
    {
        _timeOutDate = DateTime.Now.Add(handlerTimeout);
    }
}