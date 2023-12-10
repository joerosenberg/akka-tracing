using Akka.Actor;
using Akka.Event;

namespace AkkaTracing.Extension;

public class ActorTracingExtension : ExtensionIdProvider<ActorTracing>
{
    public override ActorTracing CreateExtension(ExtendedActorSystem system)
    {
        var actorTracing = new ActorTracing(system);
        var actorTelemetrySubscriber = new ActorTelemetrySubscriber(actorTracing);
        system.EventStream.Subscribe<IActorTelemetryEvent>(actorTelemetrySubscriber);
        system.ActorPipelineResolver.Register(new TracingActorProducerPipelinePlugin(actorTracing));
        return actorTracing;
    }
}