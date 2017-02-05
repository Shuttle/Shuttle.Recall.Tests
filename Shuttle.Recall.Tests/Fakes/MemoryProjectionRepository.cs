namespace Shuttle.Recall.Tests
{
    public class MemoryProjectionRepository : IProjectionRepository
    {
        public long GetSequenceNumber(string projectionName)
        {
            return 0;
        }

        public void SetSequenceNumber(string projectionName, long sequenceNumber)
        {
        }
    }
}