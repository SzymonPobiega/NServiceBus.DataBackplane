using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Backplane;
using NServiceBus.Backplane.FileSystem;

namespace Publisher2
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var busConfig = new BusConfiguration();
            busConfig.EndpointName("Publisher");
            busConfig.ScaleOut().UniqueQueuePerEndpointInstance("2");
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.EnableDataBackplane<FileSystemBackplane>();
            //busConfig.EnableDataBackplane<SqlServerBackplane>("Data Source=(local);Initial Catalog=Backplane2;Integrated Security=True");
            //busConfig.EnableDataBackplane<ConsulBackplane>("http://127.0.0.1:8500");
            busConfig.Routing().EnableAutomaticRouting();

            var endpoint = await Endpoint.Start(busConfig);

            Console.WriteLine("Press <enter> to publish an event.");

            while (true)
            {
                var line = Console.ReadLine();
                if (line == "X")
                {
                    break;
                }
                await endpoint.CreateBusContext().Publish(new SomeEvent());
            }

            await endpoint.Stop();
        }
    }

    public class SomeCommandHandler : IHandleMessages<SomeCommand>
    {
        public Task Handle(SomeCommand message, IMessageHandlerContext context)
        {
            Console.WriteLine("2: Got command");
            return Task.FromResult(0);
        }
    }
}
