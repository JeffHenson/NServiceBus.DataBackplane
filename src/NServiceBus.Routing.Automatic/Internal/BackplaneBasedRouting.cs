using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Unicast;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

namespace NServiceBus.Routing.Automatic.Internal
{
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

            context.RegisterStartupTask(builder => new HandledMessageInfoSubscriber(dataBackplane: builder.Build<IDataBackplaneClient>(),
                                                                                    subscriptionStorage: builder.Build<ISubscriptionStorage>(),
                                                                                    settings: context.Settings));
        }

        private static List<Type> GetMessageTypesHandledByThisEndpoint(MessageHandlerRegistry handlerRegistry, Conventions conventions)
        {
            return handlerRegistry.GetMessageTypes()
                                  .Where(t => !conventions.IsInSystemConventionList(t))
                                  .ToList();
        }
    }
}