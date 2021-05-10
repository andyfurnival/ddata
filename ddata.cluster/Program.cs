using System;
using System.Collections.Generic;
using System.IO;
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
                }
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