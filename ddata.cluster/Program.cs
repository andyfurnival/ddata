using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using Akka.DistributedData;
using Akka.Remote;
using Akka.Bootstrap.Docker;

namespace ddata.cluster
{
    class Program
    {
        private static ActorSystem Sys1;
        private static ActorSystem ddataActorSystem;
        
       /// <summary>
       /// the HOCON is set to DEBUG logs, to show the behaviour of DData with this small dataset. If you set to WARNING, you can use key commands m, c and i to just mimic how I interact with ddata.
       /// </summary>
       /// <param name="args"></param>
        static void Main(string[] args)
       {
           var finalConfig =  ConfigurationFactory.Empty.FromEnvironment();
   
            var cfg = finalConfig.WithFallback(ConfigurationFactory.ParseString(File.ReadAllText("HOCON")))
            .BootstrapFromDocker()
            .WithFallback(DistributedData.DefaultConfig());

            Task.Run(() =>
            {

                Sys1 = ActorSystem.Create("test", cfg);
                var cluster1 = Cluster.Get(Sys1);
                cluster1.Join(Address.Parse($"akka.tcp://test@{cluster1.SelfAddress.Host}:18001"));
                cluster1.RegisterOnMemberUp(()=> StartActors(Sys1));

                var durableKey = Environment.GetEnvironmentVariable("test");
                if (!string.IsNullOrEmpty(durableKey))
                {
                    Config durable = Config.Empty;
                    durable = $"akka.cluster.distributed-data.durable.keys=[\"{durableKey}\"]";
                    var durableConfig = durable
                        .WithFallback(
                            finalConfig.WithFallback(ConfigurationFactory.ParseString(File.ReadAllText("HOCON"))))
                        .BootstrapFromDocker()
                        .WithFallback(DistributedData.DefaultConfig());
                     ddataActorSystem = ActorSystem.Create("ddata", durableConfig);
                     var clusterddata = Cluster.Get(ddataActorSystem);
                     clusterddata.Join(Address.Parse($"akka.tcp://ddata@{cluster1.SelfAddress.Host}:18001"));
                    MigrateDData(ddataActorSystem,Sys1);
                }
            });
            
            
            char command = Char.MinValue;
            while (!command.Equals('q'))
            {
                command = Console.ReadKey().KeyChar;
                switch (command)
                {
                    case 'm':
                        SendEventMessageToRegistry(Sys1);
                        break;
                    case 'c':
                        SendCloseMessageToRegistry(Sys1);
                        break;
                    case 'i':
                        QueryDDataThroughActor(Sys1);
                        break;
                    case 'k':
                        GetDDataKeys(Sys1);
                        break;
                    case 'u':
                        UpdateMaster(Sys1, ddataActorSystem);
                        break;
                }
            }

       }

       private static async void MigrateDData(ActorSystem sourceSystem, ActorSystem targetSystem)
       {
           var replicatorSrc = DistributedData.Get(sourceSystem).Replicator;
           
           var resp = await replicatorSrc.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
           
           int emptyKeyCount = 0;
           EventCounter = 0;
           foreach (var resultKey in resp.Keys.OrderBy(x=> x))
           {
               EventCounter++;
               var key = new ORSetKey<string>($"{resultKey}");

               var keyResp = await replicatorSrc.Ask<IGetResponse>(Dsl.Get(key));
               
               if (keyResp.Get(key).Elements.Count == 0) continue;

               Migrate(key, keyResp.Get(key).Elements, targetSystem);

               
           }  
           
       }

       private static void UpdateMaster(ActorSystem source, ActorSystem target)
       {
           if (source == null) return;
           if (target == null) return;

           MigrateDData(source, target);

       }

       private static async void Migrate(ORSetKey<string> key, IImmutableSet<string> elements, ActorSystem targetSystem)
       {
           var cluster = Cluster.Get(targetSystem);
           
           var replicatorDst = DistributedData.Get(targetSystem).Replicator;

           var writeConsistency = new WriteMajority(TimeSpan.FromSeconds(2));

           foreach (var element in elements)
           {
               replicatorDst.Tell(Dsl.Update(key, ORSet<string>.Empty, writeConsistency,
                   existing => existing.Add(cluster, element)));
           }
           
       }

       private static async void GetDDataKeys(ActorSystem sys1)
       {
           var replicator = DistributedData.Get(sys1).Replicator;
           
           var resp = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
           
           int emptyKeyCount = 0;
           foreach (var resultKey in resp.Keys.OrderBy(x=> x))
           {
               EventCounter++;
               var key = new ORSetKey<string>($"{resultKey}");

               var keyResp = await replicator.Ask<IGetResponse>(Dsl.Get(key));

               Console.ForegroundColor = ConsoleColor.Green;
               if (keyResp.Get(key).Elements.Count == 0) emptyKeyCount++;
                
               Console.WriteLine($"{key.Id}\t{string.Join<string>(",", keyResp.Get(key).Elements)}");
           }
       }

       private static int EventCounter = 0;
        
       static void SendEventMessageToRegistry(ActorSystem sys)
       {
           catalogRegistries[0].Tell(new CreateEventMarket(++EventCounter, "12345-0"));
       }

       static void SendCloseMessageToRegistry(ActorSystem sys)
       {
           catalogRegistries[0].Tell(new CloseEvent(--EventCounter));
       }
        
       static void QueryDDataThroughActor(ActorSystem sys)
       {
           genericActors[0].Tell(new CreateEventMarket(EventCounter, "12345-0"));
       }

       static List<IActorRef> catalogRegistries = new List<IActorRef>();
       static List<IActorRef> genericActors = new List<IActorRef>();
        
       static void StartActors(ActorSystem sys)
       {
           var replicator = DistributedData.Get(sys).Replicator;
            
           catalogRegistries.Add(sys.ActorOf(
               Props.Create(() => new CatalogRegistryActor()),
               "CatalogRegistryActor"));

           genericActors.Add(sys.ActorOf(Props.Create(() => new SomeGenericActor(replicator)), 
               "SomeActor"));
            
       }
    }
}