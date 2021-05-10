using System;
using System.Threading.Tasks;
using Akka.Actor;
using static Akka.DistributedData.Dsl;
using Akka.DistributedData;
using Akka.Event;
using Akka.Util;
using ReadLocal = Akka.DistributedData.ReadLocal;

namespace ddata.cluster
{
    /// <summary>
    /// This Actor is an example of how we'd query ddata for some data.
    /// </summary>
    internal class SomeGenericActor : ReceiveActor
    {
        private readonly IActorRef _ddataStore;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        public SomeGenericActor(IActorRef ddataStore)
        {
            _ddataStore = ddataStore;
            Receive<CreateEventMarket>(HandleMessage);
            _log.Info(Self.Path.ToString());
        }

        private void HandleMessage(CreateEventMarket selection)
        {
            var key = new ORSetKey<string>($"Event-{selection.EventId}");
            var readConsistency = ReadLocal.Instance;
            var reply = Task.Run(() =>
            {
                var result =
                    _ddataStore.Ask<IGetResponse>(Get(key, readConsistency), TimeSpan.FromSeconds(2));
                if (result.Result.IsSuccessful)
                {
                    Sender.Tell(result.Result.Get(key));
                }
            });
        }
    }
}