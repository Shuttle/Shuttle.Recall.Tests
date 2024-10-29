using System.Collections.Generic;
using System.Threading.Tasks;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.Tests.Memory.Fakes;

public class MemoryProjectionRepository : IProjectionRepository
{
    private readonly IDictionary<string, Projection> _projections = new Dictionary<string, Projection>();
    private readonly IDictionary<string, long> _sequenceNumbers = new Dictionary<string, long>();

    public async Task<Projection?> FindAsync(string name)
    {
        return await Task.FromResult(_projections.TryGetValue(name, out var projection) ? projection : null);
    }

    public async Task SaveAsync(Projection projection)
    {
        _projections.Remove(Guard.AgainstNull(projection).Name);
        _projections.Add(projection.Name, projection);

        await SetSequenceNumberAsync(projection.Name, projection.SequenceNumber);
    }

    public async Task SetSequenceNumberAsync(string projectionName, long sequenceNumber)
    {
        _sequenceNumbers.Remove(projectionName);
        _sequenceNumbers.Add(projectionName, sequenceNumber);

        await Task.CompletedTask;
    }

    public async ValueTask<long> GetSequenceNumberAsync(string projectionName)
    {
        return await Task.FromResult(_sequenceNumbers.TryGetValue(projectionName, out var number) ? number : 0);
    }
}