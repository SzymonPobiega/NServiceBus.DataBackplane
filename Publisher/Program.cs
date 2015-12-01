using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Backplane;
using NServiceBus.Backplane.FileSystem;

namespace Publisher
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
            busConfig.UsePersistence<InMemoryPersistence>();
            //busConfig.EnableDataBackplane<SqlServerBackplane>("Data Source=(local);Initial Catalog=Backplane2;Integrated Security=True");
            busConfig.EnableDataBackplane<FileSystemBackplane>();
            busConfig.Routing().EnableAutomaticRouting();

            var endoint = await Endpoint.Start(busConfig);

            Console.WriteLine("Press <enter> to publish an event.");

            while (true)
            {
                Console.ReadLine();
                await endoint.CreateBusContext().Publish(new SomeEvent());
            }
        }
    }

    public class SomeCommandHandler : IHandleMessages<SomeCommand>
    {
        public Task Handle(SomeCommand message, IMessageHandlerContext context)
        {
            Console.WriteLine("Got command");
            return Task.FromResult(0);
        }
    }
}
