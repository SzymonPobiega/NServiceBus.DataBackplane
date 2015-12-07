using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Logging;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{

    /*
    * Deactivation logic:
    * - if an endpoint shuts down properly, it updates the entry as inactive. Other endpoints deactivate rout
    * - if an ednpoint is killed, it does not deactivate its entry. Other endpoints monitor the update time of the entry and deactivate it when it times out
    * - if an endpoint is decomissioned, a correposponding entry should be delted.
    */

    public class HandledMessageInfoSubscriber : FeatureStartupTask
    {
        private static readonly ILog Logger = LogManager.GetLogger<HandledMessageInfoSubscriber>();

        private readonly IDataBackplaneClient dataBackplane;
        private readonly ReadOnlySettings settings;
        private readonly TimeSpan sweepPeriod;
        private readonly TimeSpan heartbeatTimeout;
        private IDataBackplaneSubscription subscription;
        private Dictionary<Type, HashSet<EndpointName>> endpointMap = new Dictionary<Type, HashSet<EndpointName>>();
        private Dictionary<EndpointName, HashSet<EndpointInstanceName>> instanceMap = new Dictionary<EndpointName, HashSet<EndpointInstanceName>>();
        private Dictionary<EndpointInstanceName, EndpointInstanceInfo> instanceInformation = new Dictionary<EndpointInstanceName, EndpointInstanceInfo>();
        private Timer sweepTimer;

        public HandledMessageInfoSubscriber(IDataBackplaneClient dataBackplane, ReadOnlySettings settings, TimeSpan sweepPeriod, TimeSpan heartbeatTimeout)
        {
            this.dataBackplane = dataBackplane;
            this.settings = settings;
            this.sweepPeriod = sweepPeriod;
            this.heartbeatTimeout = heartbeatTimeout;
        }

        protected override async Task OnStart(IBusContext context)
        {
            var routingTable = settings.Get<UnicastRoutingTable>();
            var endpointInstances = settings.Get<EndpointInstances>();

            routingTable.AddDynamic((list, bag) => FindDestination(list));
            endpointInstances.AddDynamic(FindInstances);
           
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

                    EndpointInstanceInfo instanceInfo;
                    if (!instanceInformation.TryGetValue(instanceName, out instanceInfo))
                    {
                        var newInstanceInformation = new Dictionary<EndpointInstanceName, EndpointInstanceInfo>(instanceInformation);
                        instanceInfo = new EndpointInstanceInfo();
                        newInstanceInformation[instanceName] = instanceInfo;
                        instanceInformation = newInstanceInformation;
                    }
                    if (deserializedData.Active)
                    {
                        instanceInfo.Activate(deserializedData.Timestamp);
                        Logger.InfoFormat("Instance {0} active (heartbeat).", instanceName);
                    }
                    else
                    {
                        instanceInfo.Deactivate();
                        Logger.InfoFormat("Instance {0} deactivated.", instanceName);
                    }
                    await UpdateCaches(endpointName, instanceName, types);
                }, 
                async e =>
                {

                    var deserializedData = JsonConvert.DeserializeObject<HandledMessageDeclaration>(e.Data);
                    var endpointName = new EndpointName(deserializedData.EndpointName);
                    var instanceName = new EndpointInstanceName(endpointName, deserializedData.UserDiscriminator,
                        deserializedData.TransportDiscriminator);

                    Logger.InfoFormat("Instance {0} removed from routing tables.", instanceName);

                    await UpdateCaches(endpointName, instanceName, new Type[0]);

                    instanceInformation.Remove(instanceName);
                });
            sweepTimer = new Timer(state =>
            {
                foreach (var info in instanceInformation)
                {
                    if (!info.Value.Sweep(DateTime.UtcNow, heartbeatTimeout))
                    {
                        Logger.InfoFormat("Instance {0} deactivated (heartbeat timeout).", info.Key);
                    }
                }
            }, null, sweepPeriod, sweepPeriod);
        }

        private Task UpdateCaches(EndpointName endpointName, EndpointInstanceName instanceName, Type[] types)
        {
            instanceMap = BuildNewInstanceMap(endpointName, instanceName);
            endpointMap = BuildNewEndpointMap(endpointName, types);
            return Task.FromResult(0);
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
                var activeInstances =
                    instances.Where(i => instanceInformation[i].State == InstanceState.Active).ToArray();
                if (activeInstances.Any())
                {
                    return activeInstances;
                }
                Logger.InfoFormat("No active instances of endpoint {0} detected. Trying to route to the inactive ones.", endpointName);
                return instances;
            }
            return Enumerable.Empty<EndpointInstanceName>();
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
            using (var waitHandle = new ManualResetEvent(false))
            {
                sweepTimer.Dispose(waitHandle);

                // TODO: Use async synchronization primitive
                waitHandle.WaitOne();
            }

            subscription.Unsubscribe();
            return Task.FromResult(0);
        }
    }
}