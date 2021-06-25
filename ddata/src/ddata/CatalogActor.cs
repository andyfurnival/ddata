using Akka.Actor;

namespace ddata
{
    public class CatalogActor : ReceiveActor
    {
        public CatalogActor()
        {
            ReceiveAny(msg =>
            {
                
            });
        }
    }
}