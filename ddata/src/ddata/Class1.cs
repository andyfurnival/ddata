using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.DistributedData;
using Akka.Util.Internal;
using SharpGaming.Betting.Bets.CatalogRegistry;

namespace ddata
{
    public static class Class1
    {
        private static async Task Main()
        {
            
            
            var cfg = ConfigurationFactory.ParseString(File.ReadAllText("HOCON"))
                .WithFallback(DistributedData.DefaultConfig());

            var originalColour = Console.ForegroundColor;
            
            var sys = ActorSystem.Create("test", cfg);
            var cluster1 = Cluster.Get(sys);
            cluster1.Join(Address.Parse($"akka.tcp://test@{cluster1.SelfAddress.Host}:4256"));
            cluster1.RegisterOnMemberUp(async () =>
            {
                var dd = DistributedData.Get(sys);
                int emptyKeyCount = 0;
                var replicator = DistributedData.Get(sys).Replicator;


                Console.WriteLine($"ddata durable is set to {dd.IsDurable}");

                var writeConsistency = new WriteMajority(TimeSpan.FromSeconds(2));


                // for (int i = 0; i < 100; i++)
                // {
                //     var registryKey = new ORDictionaryKey<string, GSet<string>>("Event");
                //     var i1 = i;
                //     replicator.Tell(Dsl.Update(registryKey, ORDictionary<string, GSet<string>>.Empty,
                //         writeConsistency,
                //         x => x.SetItem(cluster1, $"Event-{i1}",
                //             new GSet<string>(ImmutableHashSet<string>.Empty.Add($"MARKET-1234").Add("MARKET-2345")
                //                 .Add("MARKET-3456").Add("MARKET-4567")))));
                //
                //
                // }

                // Task.Delay(TimeSpan.FromSeconds(5));

                for (int i = 0; i < 10; i++)
                {
                    var registryKey = new ORDictionaryKey<string, GSet<string>>("Event");
                    var i1 = i;
                    replicator.Tell(Dsl.Update(registryKey, ORDictionary<string, GSet<string>>.Empty,
                        writeConsistency,
                        x => x.SetItem(cluster1, $"Event-{i1}",
                            new GSet<string>(ImmutableHashSet<string>.Empty.Add($"MARKET-5678").Add("MARKET-6789")
                                .Add("MARKET-8901").Add("MARKET-9012")))));


                }




                var key = new ORDictionaryKey<string, GSet<string>>($"Event");

                for (int i = 0; i < 10; i++)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    var market = await CatalogQuery.Instance.GetMarketsFromCatalog(replicator, i, sys.Log);
                    Console.WriteLine($"{market.ToList().Join(",")}");
                    
                    //     
                    // var response =
                    //     await replicator.Ask<IGetResponse>(Dsl.Get(key, ReadLocal.Instance), TimeSpan.FromSeconds(2));
                    //
                    // if (response.IsSuccessful)
                    // {
                    //     var result = response.Get(key);
                    //     var elements = result.Entries;
                    //     var market = elements
                    //         .Where(x => x.Key.Equals($"Event-5"))
                    //         .SelectMany(y => y.Value);
                    //     
                    //     Console.ForegroundColor = ConsoleColor.Green;
                    //     Console.WriteLine($"{market.ToList().Join(",")}");
                    // }
                    // else
                    // {
                    //     Console.ForegroundColor = ConsoleColor.Red;
                    //     Console.WriteLine($"Fail:{response.IsFailure}, Found:{response.IsFound}");
                    // }
                }

            });
          
            Console.ReadKey();

        }
    }
}