using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageInfoSubscriber : FeatureStartupTask
    {
        private readonly DataBackplaneClient dataBackplane;
        private readonly ReadOnlySettings settings;
        private IDisposable subscription;
        private Dictionary<Type, HashSet<EndpointName>> endpointMap = new Dictionary<Type, HashSet<EndpointName>>();
        private Dictionary<EndpointName, HashSet<EndpointInstanceName>> instanceMap = new Dictionary<EndpointName, HashSet<EndpointInstanceName>>();
        private Dictionary<Type, HashSet<EndpointInstanceName>> publisherMap = new Dictionary<Type, HashSet<EndpointInstanceName>>();
        private readonly Dictionary<EndpointInstanceName, HashSet<Type>> typesByInstance = new Dictionary<EndpointInstanceName, HashSet<Type>>();

        public HandledMessageInfoSubscriber(DataBackplaneClient dataBackplane, ReadOnlySettings settings)
        {
            this.dataBackplane = dataBackplane;
            this.settings = settings;
        }

        protected override async Task OnStart(IBusContext context)
        {
            var routingTable = settings.Get<UnicastRoutingTable>();
            var endpointInstances = settings.Get<EndpointInstances>();
            var publishers = settings.Get<Publishers>();

            routingTable.AddDynamic((list, bag) => FindDestination(list));
            endpointInstances.AddDynamic(FindInstances);
            publishers.AddDynamic(type =>
            {
                HashSet<EndpointInstanceName> typeEntry;
                if (publisherMap.TryGetValue(type, out typeEntry) && typeEntry.Any())
                {
                    return new PublisherAddress(typeEntry.First().EndpointName);
                }
                return null;
            });

            subscription = await dataBackplane.GetAllAndSubscribeToChanges("NServiceBus.HandledMessages",
                async e =>
                {
                    var deserializedData = JsonConvert.DeserializeObject<HandledMessageDeclaration>(e.Data);
                    var endpointName = new EndpointName(deserializedData.EndpointName);
                    var instanceName = new EndpointInstanceName(endpointName, deserializedData.UserDiscriminator, deserializedData.TransportDiscriminator);

                    var types =
                        deserializedData.HandledMessageTypes.Select(x => Type.GetType(x, false))
                            .Where(x => x != null)
                            .ToArray();

                    await UdateCaches(context, endpointName, instanceName, types);
                }, async e =>
                {
                    var deserializedData = JsonConvert.DeserializeObject<HandledMessageDeclaration>(e.Data);
                    var endpointName = new EndpointName(deserializedData.EndpointName);
                    var instanceName = new EndpointInstanceName(endpointName, deserializedData.UserDiscriminator,
                        deserializedData.TransportDiscriminator);

                    await UdateCaches(context, endpointName, instanceName, new Type[0]);
                });
        }

        private async Task UdateCaches(IBusContext context, EndpointName endpointName, EndpointInstanceName instanceName, Type[] types)
        {
            HashSet<Type> typesHandledByThisInstance;
            if (!typesByInstance.TryGetValue(instanceName, out typesHandledByThisInstance))
            {
                typesHandledByThisInstance = new HashSet<Type>();
                typesByInstance[instanceName] = typesHandledByThisInstance;
            }

            var addedTypes = types.Except(typesHandledByThisInstance).ToArray();
            var removedTypes = typesHandledByThisInstance.Except(types).ToArray();

            foreach (var type in types)
            {
                typesHandledByThisInstance.Add(type);
            }

            foreach (var removedType in removedTypes.Where(t => settings.Get<Conventions>().IsEventType(t)))
            {
                await context.Unsubscribe(removedType).ConfigureAwait(false);
            }

            instanceMap = BuildNewInstanceMap(endpointName, instanceName);
            endpointMap = BuildNewEndpointMap(endpointName, types);
            publisherMap = BuildNewPublisherMap(instanceName, types);

            foreach (var addedType in addedTypes.Where(t => settings.Get<Conventions>().IsEventType(t)))
            {
                await context.Subscribe(addedType).ConfigureAwait(false);
            }
        }

        private Dictionary<Type, HashSet<EndpointInstanceName>> BuildNewPublisherMap(EndpointInstanceName instanceName, Type[] types)
        {
            var newPublisherMap = new Dictionary<Type, HashSet<EndpointInstanceName>>();
            foreach (var pair in publisherMap)
            {
                var otherInstances = pair.Value.Where(x => x != instanceName);
                newPublisherMap[pair.Key] = new HashSet<EndpointInstanceName>(otherInstances);
            }

            foreach (var type in types)
            {
                HashSet<EndpointInstanceName> typeEntry;
                if (!newPublisherMap.TryGetValue(type, out typeEntry))
                {
                    typeEntry = new HashSet<EndpointInstanceName>();
                    newPublisherMap[type] = typeEntry;
                }
                typeEntry.Add(instanceName);
            }
            return newPublisherMap;
        }

        private Dictionary<EndpointName, HashSet<EndpointInstanceName>> BuildNewInstanceMap(EndpointName endpointName, EndpointInstanceName instanceName)
        {
            var newInstanceMap = new Dictionary<EndpointName, HashSet<EndpointInstanceName>>();
            foreach (var pair in instanceMap)
            {
                var otherInstances = pair.Value.Where(x => x != instanceName);
                newInstanceMap[pair.Key] = new HashSet<EndpointInstanceName>(otherInstances);
            }
            HashSet<EndpointInstanceName> endpointEntry;
            if (!newInstanceMap.TryGetValue(endpointName, out endpointEntry))
            {
                endpointEntry = new HashSet<EndpointInstanceName>();
                newInstanceMap[endpointName] = endpointEntry;
            }
            endpointEntry.Add(instanceName);
            return newInstanceMap;
        }

        private Dictionary<Type, HashSet<EndpointName>> BuildNewEndpointMap(EndpointName endpointName, Type[] types)
        {
            var newEndpointMap = new Dictionary<Type, HashSet<EndpointName>>();
            foreach (var pair in endpointMap)
            {
                var otherEndpoints = pair.Value.Where(x => x != endpointName);
                newEndpointMap[pair.Key] = new HashSet<EndpointName>(otherEndpoints);
            }

            foreach (var type in types)
            {
                HashSet<EndpointName> typeEntry;
                if (!newEndpointMap.TryGetValue(type, out typeEntry))
                {
                    typeEntry = new HashSet<EndpointName>();
                    newEndpointMap[type] = typeEntry;
                }
                typeEntry.Add(endpointName);
            }
            return newEndpointMap;
        }

        private IEnumerable<EndpointInstanceName> FindInstances(EndpointName endpointName)
        {
            HashSet<EndpointInstanceName> instances;
            if (instanceMap.TryGetValue(endpointName, out instances))
            {
                foreach (var instance in instances)
                {
                    yield return instance;
                }
            }
        }

        private IEnumerable<IUnicastRoute> FindDestination(List<Type> enclosedMessageTypes)
        {
            foreach (var type in enclosedMessageTypes)
            {
                HashSet<EndpointName> destinations;
                if (endpointMap.TryGetValue(type, out destinations))
                {
                    foreach (var endpointName in destinations)
                    {
                        yield return new UnicastRoute(endpointName);
                    }
                }
            }
        }

        protected override Task OnStop(IBusContext context)
        {
            subscription.Dispose();
            return Task.FromResult(0);
        }
    }
}