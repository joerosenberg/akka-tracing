using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;

namespace AkkaTracing.Hosting;

public static class AkkaTracingConfigurationExtensions
{
    public static readonly Config AkkaTracingConfig = ConfigurationFactory.ParseString(@"
akka {
    actor {
        default-mailbox {
            mailbox-type = ""AkkaTracing.Mailbox.TracingMailbox, AkkaTracing""
        }

        default-dispatcher {
            type = ""AkkaTracing.Dispatcher.TracingDispatcherConfigurator, AkkaTracing""
        }

        telemetry {
            enabled = true
        }
    }
    
    scheduler {
        implementation = ""AkkaTracing.Scheduler.TracingScheduler, AkkaTracing""
    }
}");
    
    public static AkkaConfigurationBuilder AddTracing(this AkkaConfigurationBuilder builder)
    {
        builder.AddSetup(BootstrapSetup.Create().WithConfig(AkkaTracingConfig));
        return builder;
    }
}