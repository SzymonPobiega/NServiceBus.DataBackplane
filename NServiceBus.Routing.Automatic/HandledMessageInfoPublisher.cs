using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NServiceBus.Backplane;
using NServiceBus.Features;
using NServiceBus.Settings;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageInfoPublisher : FeatureStartupTask
    {
        private readonly DataBackplaneClient dataBackplane;
        private readonly IReadOnlyCollection<Type> hanledMessageTypes;
        private readonly ReadOnlySettings settings;

        public HandledMessageInfoPublisher(
            DataBackplaneClient dataBackplane, 
            IReadOnlyCollection<Type> hanledMessageTypes,
            ReadOnlySettings settings)
        {
            this.dataBackplane = dataBackplane;
            this.hanledMessageTypes = hanledMessageTypes;
            this.settings = settings;
        }

        protected override Task OnStart(IBusContext context)
        {
            var data = new HandledMessageDeclaration
            {
                EndpointName = settings.EndpointName().ToString(),
                UserDiscriminator = settings.EndpointInstanceName().UserDiscriminator,
                TransportDiscriminator = settings.EndpointInstanceName().TransportDiscriminator,
                HandledMessageTypes = hanledMessageTypes.Select(m => m.AssemblyQualifiedName).ToArray()
            };

            var dataJson = JsonConvert.SerializeObject(data);
            return dataBackplane.Publish("NServiceBus.HandledMessages", dataJson);
        }
    }
}