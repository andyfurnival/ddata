namespace ddata.cluster
{
    public class CreateEventMarket 
    {
        public CreateEventMarket(long eventId, string market)
        {
            EventId = eventId;
            Market = market;
        }
        
        public long EventId { get; }
        
        public string Market { get; }
        
    }
}