using System.Diagnostics;
using Akka.Actor;
using Akka.Dispatch.SysMsg;
using AkkaTracing.Utils;
using OpenTelemetry.Trace;

namespace AkkaTracing;

internal record ActorInstanceTracingState(TracerProvider Provider, ActivitySource Source) : IDisposable
{
    private Activity? _actorCreating;
    private Activity? _actorStarting;
    private Activity? _actorStoppingOrRestarting;

    public void OnActorCreating(ActivitySource parentSource, Supervise supervise)
    {
        var currActivity = Activity.Current;
        _actorCreating = parentSource.StartActivity($"Creating {supervise.Child.GetShortActorTypeName()}", ActivityKind.Internal);
        Activity.Current = currActivity;
    }

    public void OnActorStarting(IActorRef actorRef)
    {
        if (_actorCreating is not null)
        {
            _actorStarting = Source.StartActivity(ActivityKind.Internal, name: $"Starting {actorRef.GetShortActorTypeName()}", 
                parentContext: _actorCreating.Context);
            _actorCreating.Stop();
            _actorCreating = null;
            return;
        }
        _actorStarting = Source.StartActivity($"Starting {actorRef.GetShortActorTypeName()}");
    }

    public void OnActorStarted()
    {
        _actorStarting?.Stop();
    }

    public void OnActorStoppingOrRestarting(IActorRef actorRef)
    {
        _actorStoppingOrRestarting = Source.StartActivity();
    }

    public void OnActorStopped(IActorRef actorRef)
    {
        if (_actorStoppingOrRestarting is null) return;
        _actorStoppingOrRestarting.DisplayName = $"Stopping {actorRef.GetShortActorTypeName()}";
        _actorStoppingOrRestarting.Stop();
    }

    public void OnActorRestarted(IActorRef actorRef)
    {
        if (_actorStoppingOrRestarting is null) return;
        _actorStoppingOrRestarting.DisplayName = $"Restarting {actorRef.GetShortActorTypeName()}";
        _actorStoppingOrRestarting.Stop();
    }

    public void Dispose()
    {
        Source.Dispose();
        Provider.Dispose();
    }
}