namespace ddata.cluster
{
    public class CloseEvent 
    {
        public CloseEvent(long eventId) => EventId = eventId;

        public long EventId { get; }

    }
}