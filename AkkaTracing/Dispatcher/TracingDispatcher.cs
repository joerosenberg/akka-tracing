using Akka.Actor;
using Akka.Dispatch;
using Akka.Dispatch.SysMsg;
using AkkaTracing.Extension;

namespace AkkaTracing.Dispatcher;

public class TracingDispatcher : Akka.Dispatch.Dispatcher
{
    private ActorTracing? _tracing;
    
    public TracingDispatcher(MessageDispatcherConfigurator configurator, string id, int throughput,
        long? throughputDeadlineTime, ExecutorServiceFactory executorServiceFactory, TimeSpan shutdownTimeout) : base(
        configurator, id, throughput, throughputDeadlineTime, executorServiceFactory, shutdownTimeout)
    {
    }

    public override void SystemDispatch(ActorCell cell, SystemMessage message)
    {
        if (message is Supervise supervise)
        {
            _tracing ??= cell.System.WithExtension<ActorTracing, ActorTracingExtension>();
            _tracing.OnActorSupervisionStarting(cell, supervise);
        }
        base.SystemDispatch(cell, message);
    }
}