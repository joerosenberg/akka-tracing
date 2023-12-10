using System.Collections.Concurrent;
using System.Diagnostics;
using Akka.Actor;
using AkkaTracing.Utils;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AkkaTracing;

internal class ActorTracingRepo
{
    private readonly ConcurrentDictionary<string, Lazy<ActorInstanceTracingState>> _tracingStates = new();
    
    public ActorInstanceTracingState GetOrAdd(IActorRef actorRef)
    {
        return _tracingStates.GetOrAdd(actorRef.Path.ToString(), 
            _ => new Lazy<ActorInstanceTracingState>(() => Create(actorRef))).Value;
    }

    private ActorInstanceTracingState Create(IActorRef actorRef)
    {
        var activitySourceName = actorRef.Path.ToStringWithUid();
        var actorTypeName = actorRef.GetShortActorTypeName();
        
        var actorTraceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .ConfigureResource(builder =>
            {
                builder.AddService(
                    serviceName: actorRef.Path.ToString(),
                    serviceVersion: actorTypeName,
                    serviceInstanceId: actorRef.Path.Uid.ToString(),
                    serviceNamespace: actorRef.Path.Root.ToString(),
                    autoGenerateServiceInstanceId: false
                );
            })
            .AddOtlpExporter(otlp =>
            {
                otlp.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();

        var activitySource = new ActivitySource(activitySourceName);

        return new ActorInstanceTracingState(actorTraceProvider, activitySource);
    }

    public void Remove(IActorRef actorRef)
    {
        if (_tracingStates.TryRemove(actorRef.Path.ToString(), out var tracingState) && tracingState.IsValueCreated) 
            tracingState.Value.Dispose();
    }
}