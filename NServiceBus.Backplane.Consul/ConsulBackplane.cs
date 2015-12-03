using NServiceBus.Backplane;
using NServiceBus.Backplane.Consul;

namespace NServiceBus
{
    public class ConsulBackplane : BackplaneDefinition
    {
        public override IDataBackplane CreateBackplane(string nodeId, string connectionString)
        {
            return new ConsulDataBackplane(nodeId, connectionString);
        }
    }
}