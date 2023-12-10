using Akka.Actor;
using Akka.Actor.Scheduler;

namespace AkkaTracing.Scheduler;

internal sealed class TracedScheduledTellMsg : IScheduledTellMsg
{
    public TracedScheduledTellMsg(object message)
    {
        Message = message;
    }
    
    public object Message { get; }
}

internal sealed class TracedScheduledTellMsgNoInfluenceReceiveTimeout : IScheduledTellMsg, INotInfluenceReceiveTimeout
{
    public TracedScheduledTellMsgNoInfluenceReceiveTimeout(object message)
    {
        Message = message;
    }

    public object Message { get; }
}