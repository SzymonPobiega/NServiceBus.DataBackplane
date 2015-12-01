using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;

namespace NServiceBus.Backplane
{
    /// <summary>
    /// Allows to configure data backplane.
    /// </summary>
    public static class DataBackplaneConfigExtensions
    {
        /// <summary>
        /// Enables the specified implementation of the data backplane.
        /// </summary>
        /// <typeparam name="T">Implementation type.</typeparam>
        /// <param name="busConfiguration">Config.</param>
        /// <param name="connectionString">Optional connection string. Some implementations might require it.</param>
        public static void EnableDataBackplane<T>(this BusConfiguration busConfiguration, string connectionString = null)
            where T : BackplaneDefinition, new()
        {
            busConfiguration.GetSettings().Set("NServiceBus.DataBackplane.ConnectionString", connectionString);
            busConfiguration.GetSettings().Set<BackplaneDefinition>(new T());
            busConfiguration.GetSettings().EnableFeatureByDefault(typeof (DataBackplane));
        }
    }
}