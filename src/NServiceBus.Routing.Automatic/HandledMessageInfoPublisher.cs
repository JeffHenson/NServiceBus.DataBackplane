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
        private readonly IDataBackplaneClient _dataBackplane;
        private readonly IReadOnlyCollection<Type> _hanledMessageTypes;
        private readonly ReadOnlySettings _settings;
        private readonly TimeSpan _heartbeatPeriod;
        private HandledMessageDeclaration _publication;
        private Timer _timer;

        public HandledMessageInfoPublisher(
            IDataBackplaneClient dataBackplane,
            IReadOnlyCollection<Type> hanledMessageTypes,
            ReadOnlySettings settings,
            TimeSpan heartbeatPeriod)
        {
            _dataBackplane = dataBackplane;
            _hanledMessageTypes = hanledMessageTypes;
            _settings = settings;
            _heartbeatPeriod = heartbeatPeriod;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var mainLogicalAddress = _settings.LogicalAddress();
            var instanceProperties = mainLogicalAddress.EndpointInstance.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            instanceProperties["queue"] = mainLogicalAddress.EndpointInstance.Endpoint;
            var publishedMessageTypes = _settings.Get<Type[]>("NServiceBus.AutomaticRouting.PublishedTypes");
            _publication = new HandledMessageDeclaration
                           {
                               EndpointName = _settings.EndpointName(),
                               Discriminator = mainLogicalAddress.EndpointInstance.Discriminator,
                               InstanceProperties = instanceProperties,
                               HandledMessageTypes = _hanledMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray(),
                               PublishedMessageTypes = publishedMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray()
                           };

            _timer = new Timer(state => { Publish().ConfigureAwait(false).GetAwaiter().GetResult(); }, null, _heartbeatPeriod, _heartbeatPeriod);

            return Publish();
        }

        private Task Publish()
        {
            var dataJson = JsonConvert.SerializeObject(_publication);
            return _dataBackplane.Publish("NServiceBus.HandledMessages", dataJson);
        }

        protected override Task OnStop(IMessageSession session)
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                _timer.Dispose(waitHandle);
                waitHandle.WaitOne();
            }
            return _dataBackplane.Revoke("NServiceBus.HandledMessages");
        }
    }
}