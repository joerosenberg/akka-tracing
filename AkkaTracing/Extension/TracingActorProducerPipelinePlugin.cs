using Akka.Actor;

namespace AkkaTracing.Extension;

internal class TracingActorProducerPipelinePlugin : ActorProducerPluginBase
{
    private readonly ActorTracing _actorTracing;

    public TracingActorProducerPipelinePlugin(ActorTracing actorTracing)
    {
        _actorTracing = actorTracing;
    }

    public override void AfterIncarnated(ActorBase actor, IActorContext context)
    {
        _actorTracing.OnActorInstanceCreated(actor, context);
    }

    public override void BeforeIncarnated(ActorBase actor, IActorContext context)
    {
        _actorTracing.OnActorInstanceTerminated(actor, context);
    }
}