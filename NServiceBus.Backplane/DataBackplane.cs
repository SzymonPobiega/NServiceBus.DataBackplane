using System.Threading.Tasks;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;

namespace NServiceBus.Backplane
{
    public static class DataBackplaneConfigExtensions
    {
        public static void EnableDataBackplane<T>(this BusConfiguration busConfiguration, string connectionString)
            where T : BackplaneDefinition, new()
        {
            busConfiguration.GetSettings().Set("NServiceBus.DataBackplane.ConnectionString", connectionString);
            busConfiguration.GetSettings().Set<BackplaneDefinition>(new T());
            busConfiguration.GetSettings().EnableFeatureByDefault(typeof (DataBackplane));
        }
    }

    public class DataBackplane : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var transportAddress = context.Settings.LocalAddress();
            var connectionString = context.Settings.Get<string>("NServiceBus.DataBackplane.ConnectionString");
            var definition = context.Settings.Get<BackplaneDefinition>();
            var backplane = definition.CreateBackplane(transportAddress, connectionString);
            var backplaneClient = new DataBackplaneClient(backplane, new DefaultQuerySchedule());
            context.Container.ConfigureComponent(_ => backplaneClient, DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(new DataBackplaneClientLifecycle(backplaneClient));
        }

        private class DataBackplaneClientLifecycle : FeatureStartupTask
        {
            private readonly DataBackplaneClient client;

            public DataBackplaneClientLifecycle(DataBackplaneClient client)
            {
                this.client = client;
            }

            protected override Task OnStart(IBusContext context)
            {
                return client.Start();
            }

            protected override Task OnStop(IBusContext context)
            {
                return client.Stop();
            }
        }

        
    }
}