using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class PrimitiveEventJournal
{
    public PrimitiveEvent PrimitiveEvent { get; }
    public bool Committed { get; private set; }

    public PrimitiveEventJournal(PrimitiveEvent primitiveEvent)
    {
        PrimitiveEvent = Guard.AgainstNull(primitiveEvent);
    }

    public void Commit()
    {
        Committed = true;
    }
}