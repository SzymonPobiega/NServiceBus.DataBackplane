using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Backplane;
using NServiceBus.Backplane.FileSystem;

namespace Sender
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
            //busConfig.EnableDataBackplane<SqlServerBackplane>("Data Source=(local);Initial Catalog=Backplane1;Integrated Security=True");
            //busConfig.EnableDataBackplane<FileSystemBackplane>();
            busConfig.EnableDataBackplane<ConsulBackplane>("http://127.0.0.1:8500");
            busConfig.Routing().EnableAutomaticRouting();

            var endoint = await Endpoint.Start(busConfig);

            Console.WriteLine("Press <enter> to send a command.");

            while (true)
            {
                Console.ReadLine();
                await endoint.CreateBusContext().Send(new SomeCommand());
            }
        }
    }

    public class SomeEventHandler : IHandleMessages<SomeEvent>
    {
        public Task Handle(SomeEvent message, IMessageHandlerContext context)
        {
            Console.WriteLine("Got event");
            return Task.FromResult(0);
        }
    }
}
