namespace NServiceBus.Backplane
{
    public abstract class BackplaneDefinition
    {
        public abstract IDataBackplane CreateBackplane(string ownerId, string connectionString);
    }
}