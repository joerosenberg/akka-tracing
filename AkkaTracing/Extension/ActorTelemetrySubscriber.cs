using Akka.Actor;

namespace AkkaTracing.Extension;

internal class ActorTelemetrySubscriber : MinimalActorRef
{
    private readonly ActorTracing _tracing;

    public ActorTelemetrySubscriber(ActorTracing tracing)
    {
        _tracing = tracing;
    }
    
    protected override void TellInternal(object message, IActorRef sender)
    {
        if (message is IActorTelemetryEvent actorEvent) _tracing.OnActorLifecycleEvent(actorEvent);
    }

    public override ActorPath Path => new RootActorPath(Address.AllSystems, "/AkkaTracing");
    public override IActorRefProvider Provider => throw new NotSupportedException();
}