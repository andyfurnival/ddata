using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
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
using Akka.Util.Internal;

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
           var durableKey = Environment.GetEnvironmentVariable("test");
           var finalConfig =  ConfigurationFactory.Empty.FromEnvironment();
           Config durable = $"akka.cluster.distributed-data.durable.keys=[\"{durableKey}\"]";
           
  
            var cfg = durable.WithFallback(finalConfig.WithFallback(ConfigurationFactory.ParseString(File.ReadAllText("HOCON"))))
            .BootstrapFromDocker()
            .WithFallback(DistributedData.DefaultConfig());

            Task.Run(() =>
            {

                Sys1 = ActorSystem.Create("test", cfg);
                var cluster1 = Cluster.Get(Sys1);
                cluster1.Join(Address.Parse($"akka.tcp://test@{cluster1.SelfAddress.Host}:18001"));
                cluster1.RegisterOnMemberUp(()=> StartActors(Sys1));
                
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
                }
            }

       }

       
       

       private static async void GetDDataKeys(ActorSystem sys1)
       {
           var replicator = DistributedData.Get(sys1).Replicator;
           
           var key = new ORDictionaryKey<string, GSet<string>>("Event");

           replicator.Ask(Dsl.Get(key)).ContinueWith(res =>
           {
               var result = res.Result as GetSuccess;
               var elements = ((ORDictionary<string, GSet<string>>) result.Data).Entries;
               Console.ForegroundColor = ConsoleColor.Green;
               int emptyKeyCount = 0;
               elements.OrderBy(x=>x).ForEach(e =>
               {
                   Console.ForegroundColor = ConsoleColor.Green;
                   if (e.Value.Count == 0)
                   {
                       Console.ForegroundColor = ConsoleColor.Red;
                       emptyKeyCount++;
                   }
                   
                   Console.WriteLine($"{e.Key}, {e.Value.Join(",")}");
               });
               Console.WriteLine($"There are {emptyKeyCount} empty keys");
           });
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