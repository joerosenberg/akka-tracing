using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Configuration;
using Akka.Hosting;
using AkkaTracing;
using AkkaTracing.Examples.Simple;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(TracingMailboxQueue.ActivitySourceName);
        tracing.AddZipkinExporter(zipkin =>
        {
            zipkin.Endpoint = new Uri("http://localhost:9411/api/v2/spans");
        });
    });

var akkaTracingConfig = ConfigurationFactory.ParseString("""
akka {
    actor {
        default-mailbox {
            mailbox-type: "AkkaTracing.TracingMailbox, AkkaTracing"
        }
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