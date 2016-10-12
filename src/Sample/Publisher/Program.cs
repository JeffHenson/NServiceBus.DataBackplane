using System;
using System.Threading.Tasks;
using Messages;
using NServiceBus;
using NServiceBus.Backplane;

namespace Publisher
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var busConfig = new EndpointConfiguration("Publisher");
            busConfig.OverrideLocalAddress("Publisher-1");
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.EnableDataBackplane<ConsulBackplane>();
            busConfig.EnableAutomaticRouting().AdvertisePublishing(typeof(SomeEvent));

            var endpoint = await Endpoint.Start(busConfig).ConfigureAwait(false);

            Console.WriteLine("Press <enter> to publish an event.");

            while (true)
            {
                var line = Console.ReadLine();
                if (line == "X")
                {
                    break;
                }
                await endpoint.Publish(new SomeEvent()).ConfigureAwait(false);
            }

            await endpoint.Stop().ConfigureAwait(false);
        }
    }

    public class SomeCommandHandler : IHandleMessages<SomeCommand>
    {
        public Task Handle(SomeCommand message, IMessageHandlerContext context)
        {
            Console.WriteLine("1: Got command");
            return Task.FromResult(0);
        }
    }
}