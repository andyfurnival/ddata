using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.DistributedData;

namespace ddata
{
    public static class Class1
    {
        private static async Task Main()
        {
            
            Console.WriteLine("Put the ddata mdb file in a folder called cluster-data in the application root folder. Press a key when done");
            Console.Read();
            
            var cfg = ConfigurationFactory.ParseString(File.ReadAllText("HOCON"))
                .WithFallback(DistributedData.DefaultConfig());

            var originalColour = Console.ForegroundColor;
            
            var sys = ActorSystem.Create("test", cfg);
            var dd = DistributedData.Get(sys);
            int emptyKeyCount = 0;
            int marketCount = 0, eventCount = 0, totalMarketCount = 0;
            
            var resp = await dd.Replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);

            foreach (var resultKey in resp.Keys)
            {
                var key = new ORDictionaryKey<string, GSet<string>>($"{resultKey}");

                var keyResp = await dd.Replicator.Ask<IGetResponse>(Dsl.Get(key));

                Console.ForegroundColor = ConsoleColor.Green;
                if (keyResp.Get(key).Entries.Count == 0) emptyKeyCount++;
                eventCount += keyResp.Get(key).Entries.Keys.Count();
                
                var elements = keyResp.Get(key).Entries;
                
                foreach(var @event in elements.Keys)
                {
                    marketCount = elements
                        .Where(x => x.Key.Equals($"{@event}"))
                        .SelectMany(y => y.Value).Count();

                    totalMarketCount += marketCount;
                    
                    Console.WriteLine($"{@event} has {marketCount} markets ");
                }
                
            }
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Finished loading {resp.Keys.Count} keys. There were {emptyKeyCount} empty keys");
            Console.WriteLine($"There is a total of {eventCount} events and {totalMarketCount} markets.");
            Console.ForegroundColor = originalColour;
        }
    }
}