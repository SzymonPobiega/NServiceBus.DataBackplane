using NServiceBus.Backplane;
using NServiceBus.Backplane.SqlServer;

namespace NServiceBus
{
    public class SqlServerBackplane : BackplaneDefinition
    {
        public override IDataBackplane CreateBackplane(string ownerId, string connectionString)
        {
            return new SqlServerDataBackplane(ownerId, connectionString);
        }
    }
}