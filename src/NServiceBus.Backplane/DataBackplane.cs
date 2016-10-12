using System.Threading.Tasks;
using NServiceBus.Backplane.Internal;
using NServiceBus.Features;

namespace NServiceBus.Backplane
{
    /// <summary>
    ///     Represents the data backplane feature. Should not be enabled directly. Instead use
    ///     <see cref="DataBackplaneConfigExtensions.EnableDataBackplane{T}" />
    /// </summary>
    public class DataBackplane : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var transportAddress = context.Settings.LocalAddress();
            var connectionString = context.Settings.GetOrDefault<string>("NServiceBus.DataBackplane.ConnectionString");
            var definition = context.Settings.Get<BackplaneDefinition>();
            var backplane = definition.CreateBackplane(transportAddress, connectionString);
            var backplaneClient = new DataBackplaneClient(backplane, new DefaultQuerySchedule());
            context.Container.ConfigureComponent(_ => backplaneClient, DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(new DataBackplaneClientLifecycle(backplaneClient));
        }

        private class DataBackplaneClientLifecycle : FeatureStartupTask
        {
            private readonly DataBackplaneClient client;

            public DataBackplaneClientLifecycle(DataBackplaneClient client)
            {
                this.client = client;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return client.Start();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return client.Stop();
            }
        }
    }
}