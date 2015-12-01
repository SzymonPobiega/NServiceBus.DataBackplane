using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NServiceBus.Backplane;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Features;
using NServiceBus.Routing.Automatic;
using NServiceBus.Unicast;

namespace NServiceBus
{
    public static class AutomaticRoutingConfigExtensions
    {
        public static void EnableAutomaticRouting(this RoutingSettings settings)
        {
            settings.GetSettings().EnableFeatureByDefault(typeof (BackplaneBasedRouting));
            settings.GetSettings().Set(typeof(AutoSubscribe).FullName, FeatureState.Disabled);
        }
    }

    class BackplaneBasedRouting : Feature
    {
        public BackplaneBasedRouting()
        {
            DependsOn<DataBackplane>();  
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var conventions = context.Settings.Get<Conventions>();

            context.RegisterStartupTask(b =>
            {
                var handlerRegistry = b.Build<MessageHandlerRegistry>();

                var messageTypesHandled = GetMessageTypesHandledByThisEndpoint(handlerRegistry, conventions);

                return new HandledMessageInfoPublisher(b.Build<IDataBackplaneClient>(), messageTypesHandled, context.Settings);
            });

            context.RegisterStartupTask(b => new HandledMessageInfoSubscriber(b.Build<IDataBackplaneClient>(), context.Settings));
        }

        static List<Type> GetMessageTypesHandledByThisEndpoint(MessageHandlerRegistry handlerRegistry, Conventions conventions)
        {
            var messageTypesHandled = handlerRegistry.GetMessageTypes()//get all potential messages
                .Where(t => !conventions.IsInSystemConventionList(t)) //never auto-route system messages
                .ToList();

            return messageTypesHandled;
        }
    }
}
