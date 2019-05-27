using System;

namespace Shuttle.Recall.Tests
{
    public class OrderHandler :
        IEventHandler<ItemAdded>,
        IEventHandler<OrderSnapshot>
    {
        private int _count;
        private DateTime _timeOutDate = DateTime.MaxValue;

        public bool IsComplete => _count == 5;
        public bool HasTimedOut => _timeOutDate < DateTime.Now;

        public void ProcessEvent(IEventHandlerContext<ItemAdded> context)
        {
            _count++;
        }

        public void ProcessEvent(IEventHandlerContext<OrderSnapshot> context)
        {
            _count++;
        }

        public void Start(int handlerTimeoutSeconds)
        {
            _timeOutDate = DateTime.Now.AddSeconds(handlerTimeoutSeconds < 5 ? 5 : handlerTimeoutSeconds);
        }
    }
}