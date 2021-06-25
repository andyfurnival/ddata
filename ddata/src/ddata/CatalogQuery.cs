using Akka.Actor;
using Akka.DistributedData;
using Akka.Event;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SharpGaming.Betting.Bets.CatalogRegistry
{
    public class CatalogQuery
    {
         static CatalogQuery()
        {
            if (Instance == null) Instance = new CatalogQuery();
        }

        public static CatalogQuery Instance = null;
        
        private  Dictionary<long, List<string>> _localStore = new ();
        public async Task<IEnumerable<string>> GetMarketsFromCatalog(IActorRef catalogDataStore, long eventId, ILoggingAdapter logger)
        {
            var registryKey = new ORDictionaryKey<string, GSet<string>>("Event");
            try
            {
                if (_localStore.ContainsKey(eventId))
                {
                    var items =  _localStore[eventId].ToArray();
                    logger.Info("found in local cache");
                    return items;
                }
                logger.Info("query ddata for event {0}",eventId);
                var response = await catalogDataStore.Ask<IGetResponse>(Dsl.Get(registryKey, ReadLocal.Instance),TimeSpan.FromSeconds(2));
                
                if (response == null)
                {
                    logger.Info("hardcoded value returned");
                    return new List<string>(){"10002-0"};
                }
                
                logger.Info("query ddata for event {3} Fail:{0}, Success:{1}, Found:{2}",response.IsFailure, response.IsSuccessful, response.IsFound, eventId);
                if (response.IsSuccessful)
                {
                    var res = response.Get(registryKey);
                    var elements = res.Entries;
                    var market = elements
                        .Where(x => x.Key.Equals($"Event-{eventId}"))
                        .SelectMany(y => y.Value);

                    var marketsFromCatalog = market.ToList();
                    if (marketsFromCatalog.Any())
                    {
                        logger.Info("addding to local store");
                        _localStore.Add(eventId, marketsFromCatalog);
                        if (_localStore.Count >= 5)
                        {
                            var pairsToRemove = _localStore.OrderBy(x => x.Key)
                                .Take(1);

                            foreach (var pair in pairsToRemove)
                            {
                                logger.Info("removing older keys from local store {0}", pair.Key);
                                _localStore.Remove(pair.Key);
                            }
                        }
                        
                        
                    }
                    logger.Info("returning {0} markets", marketsFromCatalog.Count);
                    return marketsFromCatalog;
                }
                logger.Info("unexpected result {0}", response);
            }
            catch(Exception e)
            {
                // ignored
                logger.Error("unexpected error {0}", e);
            }

            return new List<string>();
        }
    }
}