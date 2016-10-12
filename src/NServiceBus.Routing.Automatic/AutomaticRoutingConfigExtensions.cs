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
            var settings = endpointConfiguration.GetSettings();
            settings.EnableFeatureByDefault(typeof(BackplaneBasedRouting));
            settings.Set(typeof(AutoSubscribe).FullName, FeatureState.Disabled);
            return new AutomaticRoutingSettings(settings);
        }
    }
}