using System;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{
    public class AutomaticRoutingSettings : ExposeSettings
    {
        public AutomaticRoutingSettings(SettingsHolder settings) : base(settings) {}

        public void AdvertisePublishing(params Type[] publishedTypes)
        {
            this.GetSettings().Set("NServiceBus.AutomaticRouting.PublishedTypes", publishedTypes);
        }
    }
}