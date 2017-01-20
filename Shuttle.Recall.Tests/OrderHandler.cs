using System;

namespace Shuttle.Recall.Tests
{
    public class OrderHandler :
        IEventHandler<ItemAdded>,
        IEventHandler<OrderSnapshot>
    {
        private int _count;
        private DateTime _timeOutDate = DateTime.MaxValue;

        public bool IsComplete
        {
            get { return _count == 5; }
        }

        public bool HasTimedOut
        {
            get { return _timeOutDate < DateTime.Now; }
        }

        public void ProcessEvent(IEventHandlerContext<ItemAdded> context)
        {
            _count++;
        }

        public void ProcessEvent(IEventHandlerContext<OrderSnapshot> context)
        {
            _count++;
        }

        public void Start()
        {
            _timeOutDate = DateTime.Now.AddSeconds(60);
        }
    }
}