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

        protected override Task OnStart(IBusContext context)
        {
            publication = new HandledMessageDeclaration
            {
                EndpointName = settings.EndpointName().ToString(),
                UserDiscriminator = settings.EndpointInstanceName().UserDiscriminator,
                TransportDiscriminator = settings.EndpointInstanceName().TransportDiscriminator,
                HandledMessageTypes = hanledMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray(),
                Active = true,
            };

            timer = new Timer(state =>
            {
                Publish().ConfigureAwait(false).GetAwaiter().GetResult();
            }, null, heartbeatPeriod, heartbeatPeriod);

            return Publish();
        }

        private Task Publish()
        {
            publication.Timestamp = DateTime.UtcNow;
            var dataJson = JsonConvert.SerializeObject(publication);
            return dataBackplane.Publish("NServiceBus.HandledMessages", dataJson);
        }

        protected override Task OnStop(IBusContext context)
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                timer.Dispose(waitHandle);

                // TODO: Use async synchronization primitive
                waitHandle.WaitOne();
            }

            publication.Active = false;
            return Publish();
        }
    }
}