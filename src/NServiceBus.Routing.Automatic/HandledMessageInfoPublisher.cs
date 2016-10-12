using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageInfoPublisher : FeatureStartupTask
    {
        private readonly IDataBackplaneClient dataBackplane;
        private readonly IReadOnlyCollection<Type> hanledMessageTypes;
        private readonly ReadOnlySettings settings;
        private readonly TimeSpan heartbeatPeriod;
        private HandledMessageDeclaration publication;
        private Timer timer;

        public HandledMessageInfoPublisher(
            IDataBackplaneClient dataBackplane,
            IReadOnlyCollection<Type> hanledMessageTypes,
            ReadOnlySettings settings,
            TimeSpan heartbeatPeriod)
        {
            this.dataBackplane = dataBackplane;
            this.hanledMessageTypes = hanledMessageTypes;
            this.settings = settings;
            this.heartbeatPeriod = heartbeatPeriod;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var mainLogicalAddress = settings.LogicalAddress();
            var instanceProperties = mainLogicalAddress.EndpointInstance.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            instanceProperties["queue"] = mainLogicalAddress.EndpointInstance.Endpoint;
            var publishedMessageTypes = settings.Get<Type[]>("NServiceBus.AutomaticRouting.PublishedTypes");
            publication = new HandledMessageDeclaration
                          {
                              EndpointName = settings.EndpointName(),
                              Discriminator = mainLogicalAddress.EndpointInstance.Discriminator,
                              InstanceProperties = instanceProperties,
                              HandledMessageTypes = hanledMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray(),
                              PublishedMessageTypes = publishedMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray()
                          };

            timer = new Timer(state => { Publish().ConfigureAwait(false).GetAwaiter().GetResult(); }, null, heartbeatPeriod, heartbeatPeriod);

            return Publish();
        }

        private Task Publish()
        {
            var dataJson = JsonConvert.SerializeObject(publication);
            return dataBackplane.Publish("NServiceBus.HandledMessages", dataJson);
        }

        protected override Task OnStop(IMessageSession session)
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                timer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
            return dataBackplane.Revoke("NServiceBus.HandledMessages");
        }
    }
}