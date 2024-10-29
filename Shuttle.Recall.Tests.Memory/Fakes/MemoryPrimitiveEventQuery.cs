using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryPrimitiveEventQuery : IPrimitiveEventQuery
{
    private readonly Dictionary<Guid, List<PrimitiveEvent>> _store;

    public MemoryPrimitiveEventQuery(Dictionary<Guid, List<PrimitiveEvent>> store)
    {
        _store = Guard.AgainstNull(store);
    }

    public async Task<IEnumerable<PrimitiveEvent>> SearchAsync(PrimitiveEvent.Specification specification)
    {
        Guard.AgainstNull(specification);

        var all = new List<PrimitiveEvent>();

        foreach (var events in _store.Values)
        {
            all.AddRange(events);
        }

        var queryable = all.AsQueryable();

        queryable = queryable.Where(item => item.SequenceNumber <= specification.SequenceNumberStart + (specification.Count > 0 ? specification.Count : 1));


        if (specification.SequenceNumberStart > 0)
        {
            queryable = queryable.Where(item => item.SequenceNumber >= specification.SequenceNumberStart);
        }

        if (specification.Ids.Any())
        {
            queryable = queryable.Where(item => specification.Ids.Contains(item.Id));
        }

        if (specification.EventTypes.Any())
        {
            var types = specification.EventTypes.Select(type => type.FullName);

            queryable = queryable.Where(item => types.Contains(item.EventType));
        }

        return await Task.FromResult(queryable.ToList());
    }
}