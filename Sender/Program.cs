using System;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Backplane;
using NServiceBus.Backplane.FileSystem;

namespace Sender
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var busConfig = new EndpointConfiguration("Sender");
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.EnableDataBackplane<FileSystemBackplane>();
            //busConfig.EnableDataBackplane<SqlServerBackplane>("Data Source=(local);Initial Catalog=Backplane1;Integrated Security=True");
            //busConfig.EnableDataBackplane<ConsulBackplane>("http://127.0.0.1:8500");
            busConfig.EnableAutomaticRouting();

            var endpoint = await Endpoint.Start(busConfig).ConfigureAwait(false);

            Console.WriteLine("Press <enter> to send a command.");

            while (true)
            {
                var line = Console.ReadLine();
                if (line == "X")
                {
                    break;
                }
                await endpoint.Send(new SomeCommand()).ConfigureAwait(false);
            }

            await endpoint.Stop().ConfigureAwait(false);
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