using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Akka.Actor;
using Akka.Actor.Internal;
using Akka.Dispatch.SysMsg;

namespace AkkaTracing;

public class ActorTracing : IExtension
{
    private readonly ActorTracingRepo _tracingRepo;

    public ActorTracing(ActorSystem system)
    {
        _tracingRepo = new ActorTracingRepo();
    }

    internal ActivitySource Get()
    {
        var currActor = InternalCurrentActorCellKeeper.Current;
        if (currActor is null) throw new NotSupportedException("Not inside an actor cell");
        return _tracingRepo.GetOrAdd(currActor.Self).Source;
    }

    internal bool TryGetCurrentActorSource([NotNullWhen(true)] out ActivitySource? source)
    {
        var currActor = InternalCurrentActorCellKeeper.Current;
        if (currActor is null)
        {
            source = null;
            return false;
        }

        source = _tracingRepo.GetOrAdd(currActor.Self).Source;
        return true;
    }
    
    internal ActivitySource Get(IActorRef actorRef)
    {
        return _tracingRepo.GetOrAdd(actorRef).Source;
    }

    internal void OnActorSupervisionStarting(ActorCell supervisor, Supervise supervise)
    {
        _tracingRepo.GetOrAdd(supervise.Child).OnActorCreating(_tracingRepo.GetOrAdd(supervisor.Self).Source, supervise);
    }

    internal void OnActorInstanceCreated(ActorBase actor, IActorContext context)
    {
        _tracingRepo.GetOrAdd(context.Self).OnActorStarting(context.Self);
    }
    
    internal void OnActorInstanceTerminated(ActorBase actor, IActorContext context)
    {
        _tracingRepo.GetOrAdd(context.Self).OnActorStoppingOrRestarting(context.Self);
    }

    internal void OnActorLifecycleEvent(IActorTelemetryEvent msg)
    {
        switch (msg)
        {
            case ActorStarted actorStarted:
                _tracingRepo.GetOrAdd(actorStarted.Subject).OnActorStarted();
                return;
            case ActorRestarted actorRestarted:
                _tracingRepo.GetOrAdd(actorRestarted.Subject).OnActorRestarted(actorRestarted.Subject);
                _tracingRepo.Remove(actorRestarted.Subject);
                return;
            case ActorStopped actorStopped:
                _tracingRepo.GetOrAdd(actorStopped.Subject).OnActorStopped(actorStopped.Subject);
                _tracingRepo.Remove(actorStopped.Subject);
                return;
        }
    }
}