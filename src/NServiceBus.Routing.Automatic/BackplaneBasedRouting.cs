using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus.Backplane;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;
using NServiceBus.Routing.Automatic;
using NServiceBus.Settings;
using NServiceBus.Unicast;

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

    public class AutomaticRoutingSettings : ExposeSettings
    {
        public AutomaticRoutingSettings(SettingsHolder settings) : base(settings) {}

        public void AdvertisePublishing(params Type[] publishedTypes)
        {
            this.GetSettings().Set("NServiceBus.AutomaticRouting.PublishedTypes", publishedTypes);
        }
    }

    internal class BackplaneBasedRouting : Feature
    {
        public BackplaneBasedRouting()
        {
            DependsOn<DataBackplane>();
            Defaults(s => s.SetDefault("NServiceBus.AutomaticRouting.PublishedTypes", new Type[0]));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var conventions = context.Settings.Get<Conventions>();

            context.RegisterStartupTask(builder =>
                                        {
                                            var handlerRegistry = builder.Build<MessageHandlerRegistry>();
                                            var messageTypesHandled = GetMessageTypesHandledByThisEndpoint(handlerRegistry, conventions);
                                            return new HandledMessageInfoPublisher(dataBackplane: builder.Build<IDataBackplaneClient>(),
                                                                                   hanledMessageTypes: messageTypesHandled,
                                                                                   settings: context.Settings,
                                                                                   heartbeatPeriod: TimeSpan.FromSeconds(5));
                                        });

            context.RegisterStartupTask(builder =>
                                        {
                                            var handlerRegistry = builder.Build<MessageHandlerRegistry>();
                                            var messageTypesHandled = GetMessageTypesHandledByThisEndpoint(handlerRegistry, conventions);
                                            return new HandledMessageInfoSubscriber(dataBackplane: builder.Build<IDataBackplaneClient>(),
                                                                                    settings: context.Settings,
                                                                                    hanledMessageTypes: messageTypesHandled);
                                        });
        }

        private static List<Type> GetMessageTypesHandledByThisEndpoint(MessageHandlerRegistry handlerRegistry, Conventions conventions)
        {
            return handlerRegistry.GetMessageTypes() //get all potential messages
                                  .Where(t => !conventions.IsInSystemConventionList(t)) //never auto-route system messages
                                  .ToList();
        }
    }
}