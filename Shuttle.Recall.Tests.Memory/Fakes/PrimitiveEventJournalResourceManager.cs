using System;
using System.Collections.Generic;
using System.Transactions;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class PrimitiveEventJournalResourceManager : IEnlistmentNotification
{
    private readonly IPrimitiveEventStore _primitiveEventStore;
    private readonly List<PrimitiveEventJournal> _primitiveEventJournals;

    public PrimitiveEventJournalResourceManager(IPrimitiveEventStore primitiveEventStore, List<PrimitiveEventJournal> primitiveEventJournals)
    {
        _primitiveEventStore = Guard.AgainstNull(primitiveEventStore);
        _primitiveEventJournals = Guard.AgainstNull(primitiveEventJournals);
    }

    public void Commit(Enlistment enlistment)
    {
        foreach (var primitiveEventJournal in _primitiveEventJournals)
        {
            primitiveEventJournal.Commit();
        }

        Guard.AgainstNull(enlistment).Done();
    }

    public void InDoubt(Enlistment enlistment)
    {
        Guard.AgainstNull(enlistment).Done();
    }

    public void Prepare(PreparingEnlistment preparingEnlistment)
    {
        Guard.AgainstNull(preparingEnlistment).Prepared();
    }

    public void Rollback(Enlistment enlistment)
    {
        foreach (var primitiveEventJournal in _primitiveEventJournals)
        {
            _primitiveEventStore.RemoveEventAsync(primitiveEventJournal.PrimitiveEvent.Id, primitiveEventJournal.PrimitiveEvent.EventId).Wait();
        }

        Guard.AgainstNull(enlistment).Done();
    }
}