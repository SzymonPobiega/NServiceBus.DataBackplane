using System;

namespace NServiceBus.Routing.Automatic
{
    public class HandledMessageDeclaration
    {
        public string EndpointName { get; set; }

        public string UserDiscriminator { get; set; }

        public string TransportDiscriminator { get; set; }

        public string[] HandledMessageTypes { get; set; }

        public bool Active { get; set; }

        public DateTime Timestamp { get; set; }
    }
}