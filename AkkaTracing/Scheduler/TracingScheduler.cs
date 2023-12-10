using System.Diagnostics;
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Event;

namespace AkkaTracing.Scheduler;

public sealed class TracingScheduler : SchedulerBase, IDateTimeOffsetNowTimeProvider, IDisposable
{
    private readonly SchedulerBase _base;

    public TracingScheduler(Config scheduler, ILoggingAdapter log) : base(scheduler, log)
    {
        _base = new HashedWheelTimerScheduler(scheduler, log);
    }

    protected override void InternalScheduleTellOnce(TimeSpan delay, ICanTell receiver, object message, IActorRef sender,
        ICancelable cancelable)
    {
        InternalScheduleOnce(delay, new TracedScheduledTell(receiver, message, sender), cancelable);
    }

    protected override void InternalScheduleTellRepeatedly(TimeSpan initialDelay, TimeSpan interval, ICanTell receiver, object message,
        IActorRef sender, ICancelable cancelable)
    {
        InternalScheduleRepeatedly(initialDelay, interval, new TracedScheduledTell(receiver, message, sender), cancelable);
    }

    protected override void InternalScheduleOnce(TimeSpan delay, Action action, ICancelable cancelable)
    {
        InternalScheduleOnce(delay, new ActionRunnable(action), cancelable);
    }

    protected override void InternalScheduleOnce(TimeSpan delay, IRunnable action, ICancelable cancelable)
    {
        _base.ScheduleOnce(delay, action, cancelable);
    }

    protected override void InternalScheduleRepeatedly(TimeSpan initialDelay, TimeSpan interval, Action action, ICancelable cancelable)
    {
        InternalScheduleRepeatedly(initialDelay, interval, new ActionRunnable(action), cancelable);
    }

    protected override void InternalScheduleRepeatedly(TimeSpan initialDelay, TimeSpan interval, IRunnable action, ICancelable cancelable)
    {
        _base.ScheduleRepeatedly(initialDelay, interval, action, cancelable);
    }

    protected override DateTimeOffset TimeNow => DateTimeOffset.UtcNow;
    public override TimeSpan MonotonicClock => _base.MonotonicClock;
    public override TimeSpan HighResMonotonicClock => _base.HighResMonotonicClock;


    public void Dispose()
    {
        if (_base is IDisposable disposable) disposable.Dispose();
    }
    
    private sealed class TracedScheduledTell : IRunnable
    {
        private readonly ICanTell _receiver;
        private readonly object _message;
        private readonly IActorRef _sender;
        private readonly Activity? _activityWhenScheduled;

        public TracedScheduledTell(ICanTell receiver, object message, IActorRef sender)
        {
            _receiver = receiver;
            _message = receiver is not ActorRefWithCell 
                ? message 
                : message is INotInfluenceReceiveTimeout 
                    ? new TracedScheduledTellMsgNoInfluenceReceiveTimeout(message) 
                    : new TracedScheduledTellMsg(message);
            _sender = sender;
            _activityWhenScheduled = Activity.Current;
        }

        public void Run()
        {
            Activity.Current = _activityWhenScheduled;

            _receiver.Tell(_message, _sender);
        }

        public override string ToString()
        {
            return $"[{_receiver}.Tell({_message}, {_sender})]";
        }

#if NET6_0_OR_GREATER
        public void Execute()
        {
            Run();
        }
#endif
    }
}