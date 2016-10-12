using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Logging;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageInfoSubscriber : FeatureStartupTask
    {
        private static readonly ILog Logger = LogManager.GetLogger<HandledMessageInfoSubscriber>();

        private readonly IDataBackplaneClient dataBackplane;
        private readonly ReadOnlySettings settings;
        private readonly IReadOnlyCollection<Type> messageTypesHandledByThisEndpoint;
        private IDataBackplaneSubscription subscription;
        private Dictionary<Type, string> endpointMap = new Dictionary<Type, string>();
        private Dictionary<string, HashSet<EndpointInstance>> instanceMap = new Dictionary<string, HashSet<EndpointInstance>>();
        private Dictionary<Type, string> publisherMap = new Dictionary<Type, string>();
        private IMessageSession messageSession;

        public HandledMessageInfoSubscriber(IDataBackplaneClient dataBackplane,
                                            ReadOnlySettings settings,
                                            IReadOnlyCollection<Type> hanledMessageTypes)
        {
            this.dataBackplane = dataBackplane;
            this.settings = settings;
            messageTypesHandledByThisEndpoint = hanledMessageTypes;
        }

        protected override async Task OnStart(IMessageSession session)
        {
            messageSession = session;
            subscription = await dataBackplane.GetAllAndSubscribeToChanges("NServiceBus.HandledMessages", OnChanged, OnRemoved).ConfigureAwait(false);
        }

        private async Task OnChanged(Entry e)
        {
            var deserializedData = JsonConvert.DeserializeObject<HandledMessageDeclaration>(e.Data);
            var instanceName = new EndpointInstance(deserializedData.EndpointName, deserializedData.Discriminator, deserializedData.InstanceProperties);

            var handledTypes = deserializedData.HandledMessageTypes.Select(x => Type.GetType(x, false)).Where(x => x != null).ToArray();

            var publishedTypes = deserializedData.PublishedMessageTypes.Select(x => Type.GetType(x, false)).Where(x => x != null).ToArray();

            await UpdateCaches(instanceName, handledTypes, publishedTypes).ConfigureAwait(false);
        }

        private async Task OnRemoved(Entry e)
        {
            var deserializedData = JsonConvert.DeserializeObject<HandledMessageDeclaration>(e.Data);
            var instanceName = new EndpointInstance(deserializedData.EndpointName, deserializedData.Discriminator, deserializedData.InstanceProperties);

            Logger.InfoFormat("Instance {0} removed from routing tables.", instanceName);

            await UpdateCaches(instanceName, new Type[0], new Type[0]).ConfigureAwait(false);
        }

        protected override Task OnStop(IMessageSession session)
        {
            subscription.Unsubscribe();
            return Task.FromResult(0);
        }

        private async Task UpdateCaches(EndpointInstance instanceName, Type[] handledTypes, Type[] publishedTypes)
        {
            var routingTable = settings.Get<UnicastRoutingTable>();
            var publishers = settings.Get<Publishers>();
            var endpointInstances = settings.Get<EndpointInstances>();

            var newInstanceMap = BuildNewInstanceMap(instanceName, instanceMap, handledTypes.Length == 0);
            var newEndpointMap = BuildNewEndpointMap(instanceName.Endpoint, handledTypes, endpointMap);
            var newPublisherMap = BuildNewPublisherMap(instanceName, publishedTypes, publisherMap);

            LogChangesToEndpointMap(endpointMap, newEndpointMap);
            LogChangesToInstanceMap(instanceMap, newInstanceMap);
            var toSubscribe = LogChangesToPublisherMap(publisherMap, newPublisherMap).ToArray();

            routingTable.AddOrReplaceRoutes("AutomaticRouting", newEndpointMap.Select(x => new RouteTableEntry(x.Key, UnicastRoute.CreateFromEndpointName(x.Value)))
                                                                              .ToList());
            publishers.AddOrReplacePublishers("AutomaticRouting", newPublisherMap.Select(x => new PublisherTableEntry(x.Key, PublisherAddress.CreateFromEndpointName(x.Value)))
                                                                                 .ToList());
            endpointInstances.AddOrReplaceInstances("AutomaticRouting", newInstanceMap.SelectMany(x => x.Value)
                                                                                      .ToList());

            instanceMap = newInstanceMap;
            endpointMap = newEndpointMap;
            publisherMap = newPublisherMap;

            foreach (var type in toSubscribe.Intersect(messageTypesHandledByThisEndpoint))
            {
                await messageSession.Subscribe(type)
                                    .ConfigureAwait(false);
            }
        }

        private Dictionary<string, HashSet<EndpointInstance>> BuildNewInstanceMap(EndpointInstance instanceName, Dictionary<string, HashSet<EndpointInstance>> instanceMap, bool removing)
        {
            var newInstanceMap = new Dictionary<string, HashSet<EndpointInstance>>();
            foreach (var pair in instanceMap)
            {
                var otherInstances = pair.Value.Where(x => x != instanceName);
                newInstanceMap[pair.Key] = new HashSet<EndpointInstance>(otherInstances);
            }
            if (!removing)
            {
                HashSet<EndpointInstance> endpointEntry;
                var endpointName = instanceName.Endpoint;
                if (!newInstanceMap.TryGetValue(endpointName, out endpointEntry))
                {
                    endpointEntry = new HashSet<EndpointInstance>();
                    newInstanceMap[endpointName] = endpointEntry;
                }
                endpointEntry.Add(instanceName);
            }
            return newInstanceMap;
        }

        private static Dictionary<Type, string> BuildNewEndpointMap(string endpointName, Type[] types, Dictionary<Type, string> endpointMap)
        {
            var newEndpointMap = new Dictionary<Type, string>(endpointMap);

            foreach (var type in types)
            {
                newEndpointMap[type] = endpointName;
            }
            return newEndpointMap;
        }

        private static Dictionary<Type, string> BuildNewPublisherMap(EndpointInstance endpointInstance, Type[] publishedTypes, Dictionary<Type, string> publisherMap)
        {
            var newPublisherMap = new Dictionary<Type, string>();
            var endpointName = endpointInstance.Endpoint;
            foreach (var pair in publisherMap)
            {
                if (pair.Value != endpointName)
                {
                    newPublisherMap[pair.Key] = pair.Value;
                }
            }
            foreach (var publishedType in publishedTypes)
            {
                if (!newPublisherMap.ContainsKey(publishedType))
                {
                    newPublisherMap[publishedType] = endpointName;
                }
            }
            return newPublisherMap;
        }

        private static void LogChangesToEndpointMap(Dictionary<Type, string> endpointMap, Dictionary<Type, string> newEndpointMap)
        {
            foreach (var addedType in newEndpointMap.Keys.Except(endpointMap.Keys))
            {
                Logger.Info($"Added route for {addedType.Name} to [{newEndpointMap[addedType]}]");
            }

            foreach (var removedType in endpointMap.Keys.Except(newEndpointMap.Keys))
            {
                Logger.Info($"Removed route for {removedType.Name} to [{newEndpointMap[removedType]}]");
            }

            foreach (var existingType in endpointMap.Keys.Intersect(newEndpointMap.Keys))
            {
                var newSet = newEndpointMap[existingType];
                var currentSet = endpointMap[existingType];
                if (newSet != currentSet)
                {
                    Logger.Info($"Changed route for {existingType.Name} from {currentSet} to [{newSet}]");
                }
            }
        }

        private static void LogChangesToInstanceMap(Dictionary<string, HashSet<EndpointInstance>> instanceMap, Dictionary<string, HashSet<EndpointInstance>> newInstanceMap)
        {
            foreach (var addedEndpoint in newInstanceMap.Keys.Except(instanceMap.Keys))
            {
                Logger.Info($"Added endpoint {addedEndpoint} with instances {FormatSet(newInstanceMap[addedEndpoint])}");
            }

            foreach (var removedEndpoint in instanceMap.Keys.Except(newInstanceMap.Keys))
            {
                Logger.Info($"Removed endpoint {removedEndpoint} with instances {FormatSet(instanceMap[removedEndpoint])}");
            }

            foreach (var existingEndpoint in instanceMap.Keys.Intersect(newInstanceMap.Keys))
            {
                var addedInstances = newInstanceMap[existingEndpoint].Except(instanceMap[existingEndpoint]).ToArray();
                var removedInstances = instanceMap[existingEndpoint].Except(newInstanceMap[existingEndpoint]).ToArray();
                if (addedInstances.Any())
                {
                    Logger.Info($"Added instances {FormatSet(addedInstances)} to endpoint {existingEndpoint}");
                }
                if (removedInstances.Any())
                {
                    Logger.Info($"Removed instances {FormatSet(removedInstances)} from endpoint {existingEndpoint}");
                }
            }
        }

        private static IEnumerable<Type> LogChangesToPublisherMap(Dictionary<Type, string> publisherMap, Dictionary<Type, string> newPublisherMap)
        {
            foreach (var addedType in newPublisherMap.Keys.Except(publisherMap.Keys))
            {
                Logger.Info($"Added {newPublisherMap[addedType]} as publisher of {addedType}.");
                yield return addedType;
            }

            foreach (var removedType in publisherMap.Keys.Except(newPublisherMap.Keys))
            {
                Logger.Info($"Removed {publisherMap[removedType]} as publisher of {removedType}.");
            }
        }

        private static string FormatSet(IEnumerable<object> set)
        {
            return string.Join(", ", set.Select(o => $"[{o.ToString()}]"));
        }
    }
}