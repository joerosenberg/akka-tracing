using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;
using AkkaTracing.Examples.Simple;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry();

var akkaTracingConfig = ConfigurationFactory.ParseString("""
akka {
    actor {
        default-mailbox {
            mailbox-type = "AkkaTracing.Mailbox.TracingMailbox, AkkaTracing"
        }

        default-dispatcher {
            type = "AkkaTracing.Dispatcher.TracingDispatcherConfigurator, AkkaTracing"
        }

        telemetry {
            enabled = true
        }
    }
    
    scheduler {
        implementation = "AkkaTracing.Scheduler.TracingScheduler, AkkaTracing"
    }
}
""");

builder.Services.AddAkka("System", akka =>
{
    akka.AddSetup(BootstrapSetup.Create().WithConfig(akkaTracingConfig));
    akka.WithActors((system, registry, di) =>
    {
        var fooActor = system.ActorOf(di.Props<FooActor>(), nameof(FooActor));
        var barActor = system.ActorOf(di.Props<BarActor>(), nameof(BarActor));
        registry.Register<FooActor>(fooActor);
        registry.Register<BarActor>(barActor);
    });
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();