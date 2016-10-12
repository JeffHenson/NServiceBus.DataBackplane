using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;
using NServiceBus.Routing.Automatic;
using NServiceBus.Routing.Automatic.Internal;

// ReSharper disable once CheckNamespace

namespace NServiceBus
{
    public static class AutomaticRoutingConfigExtensions
    {
        public static AutomaticRoutingSettings EnableAutomaticRouting(this EndpointConfiguration endpointConfiguration)
        {
            endpointConfiguration.EnableFeature<BackplaneBasedRouting>();
            endpointConfiguration.DisableFeature<AutoSubscribe>();
            return new AutomaticRoutingSettings(endpointConfiguration.GetSettings());
        }
    }
}