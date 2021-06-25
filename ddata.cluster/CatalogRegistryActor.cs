using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.ConstrainedExecution;
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
                var writeConsistency = new WriteMajority(TimeSpan.FromSeconds(2));
                var registryKey = new ORDictionaryKey<string, GSet<string>>("Event");

                replicator.Tell(Dsl.Update(registryKey, ORDictionary<string, GSet<string>>.Empty,
                    writeConsistency,
                    x => x.SetItem(cluster, $"Event-{message.EventId}",
                        new GSet<string>(ImmutableHashSet<string>.Empty.Add(message.Market)))));


                replicator.Ask(Dsl.Get(registryKey, ReadLocal.Instance)).ContinueWith(res =>
                {
                    var result = res.Result as GetSuccess;
                    var elements = ((ORDictionary<string, GSet<string>>) result.Data).Entries;
                    var market = elements.Where(x => x.Key.Equals($"Event-{message.EventId}"))
                        .Select(x => x).FirstOrDefault();
                    
                    Sender.Tell(market);
                });
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
               
                var registryKey = new ORDictionaryKey<string, GSet<string>>("Event");

                replicator.Tell(Dsl.Update(registryKey, ORDictionary<string, GSet<string>>.Empty,
                    writeConsistency,
                    x => x.Remove(cluster, $"Event-{message.EventId}"
                        )));

                replicator.Ask(Dsl.Get(registryKey, ReadLocal.Instance)).ContinueWith(res =>
                {
                    var result = res.Result as GetSuccess;
                    var elements = ((ORDictionary<string, GSet<string>>) result.Data).Entries;
                    var market = elements.Where(x => x.Key.Equals($"Event-{message.EventId}"))
                        .Select(x => x).FirstOrDefault();

                    var outcome = (market.Key == null && market.Value == null);
             
                    
                    Sender.Tell(outcome);
                });

               
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