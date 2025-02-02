using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory;

public class ThreadPrimitiveEvent
{
    public int ManagedThreadId { get; }
    public PrimitiveEvent PrimitiveEvent { get; }

    public ThreadPrimitiveEvent(int managedThreadId, PrimitiveEvent primitiveEvent)
    {
        ManagedThreadId = managedThreadId;
        PrimitiveEvent = Guard.AgainstNull(primitiveEvent);
    }
}