using NServiceBus.Backplane;
using NServiceBus.Backplane.SqlServer;

namespace NServiceBus
{
    public class SqlServerBackplane : BackplaneDefinition
    {
        public override IDataBackplane CreateBackplane(string nodeId, string connectionString)
        {
            return new SqlServerDataBackplane(nodeId, connectionString);
        }
    }
}