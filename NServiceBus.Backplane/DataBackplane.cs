using System.Threading.Tasks;
using NServiceBus.Features;

namespace NServiceBus.Backplane
{
    /// <summary>
    /// Represents the data backplane feature. Should not be enabled directly. Instead use <see cref="DataBackplaneConfigExtensions.EnableDataBackplane{T}" />
    /// </summary>
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