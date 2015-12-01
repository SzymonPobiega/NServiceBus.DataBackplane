namespace NServiceBus.Backplane
{
    /// <summary>
    /// Defines the implementation of the backplane.
    /// </summary>
    public abstract class BackplaneDefinition
    {
        /// <summary>
        /// Creates the implementation of the backplane.
        /// </summary>
        /// <param name="nodeId">Unique ID of the node that connects to the backplane</param>
        /// <param name="connectionString">Optional user-provided connection string</param>
        public abstract IDataBackplane CreateBackplane(string nodeId, string connectionString);
    }
}