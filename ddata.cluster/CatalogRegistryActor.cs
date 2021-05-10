using System;
using Akka.Actor;
using Akka.Cluster;
using Akka.DistributedData;
using Akka.Event;

namespace ddata.cluster
{
    /// <summary>
    /// This is a realworld example of an actor that received messages and saves them to ddata. The write-majority is done to ensure most nodes are written to before returning.
    /// </summary>
    public class CatalogRegistryActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        
        public CatalogRegistryActor()
        {
            _log.Info(Self.Path.ToString());
            SetUpReceivers();
        }

        private void SetUpReceivers()
        {
            Receive<CreateEventMarket>(HandleMessage);
            Receive<CloseEvent>(HandleMessage);
        }

        private void HandleMessage(CreateEventMarket message)
        {
            try
            {

                var cluster = Cluster.Get(Context.System);
                var replicator = DistributedData.Get(Context.System).Replicator;

                var key = new ORSetKey<string>($"Event-{message.EventId}");

                var writeConsistency = new WriteMajority(TimeSpan.FromSeconds(2));

                replicator.Tell(Dsl.Update(key, ORSet<string>.Empty, writeConsistency,
                    existing => existing.Add(cluster, message.Market)));

                var localEvent =
                    replicator.Ask<IGetResponse>(Dsl.Get(key, ReadLocal.Instance));


                Sender.Tell(localEvent.Result);
            }
            catch (Exception e)
            {
                _log.Error(e, "Unable to process message CreateEventMarket for Event {0} Market {1}", message.EventId,
                    message.Market);
                Sender.Tell(
                    $"Unable to process message CreateEventMarket for Event {message.EventId} Market {message.Market}");

            }
        }
        
        //The way our data works, I opted to just removed all the key data, but leave the key intact as sometimes i need to use the key again in rare instances, but once deleted, ddata doesn't allow this.
        private void HandleMessage(CloseEvent message)
        {
            try
            {

                var replicator = DistributedData.Get(Context.System).Replicator;
                var cluster = Cluster.Get(Context.System);
                var key = new ORSetKey<string>($"Event-{message.EventId}");
                var writeConsistency = WriteLocal.Instance;
                replicator.Tell(Dsl.Update(key, ORSet<string>.Empty, writeConsistency, $"Event-{message.EventId}",
                    existing =>
                    {
                        return existing.Clear(cluster);
                    }));

                var finalResult =
                    replicator.Ask<IGetResponse>(Dsl.Get(key, ReadLocal.Instance));
                Sender.Tell(finalResult.Result);
            }
            catch (DataDeletedException e)
            {
                Sender.Tell($"Event {message.EventId} has been deleted");
            }
            catch (Exception e)
            {
                _log.Error(e, "Unable to process message CloseEvent for Event {0}", message.EventId);
                Sender.Tell($"Unable to process message CloseEvent for Event {message.EventId}");
            }
        }
    }
}