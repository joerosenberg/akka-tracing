using System.Collections.Concurrent;
using System.Diagnostics;
using Akka.Actor;
using Akka.Actor.Internal;
using Akka.Actor.Scheduler;
using Akka.Dispatch.MessageQueues;

namespace AkkaTracing.Mailbox;

internal sealed class TracingMailboxQueue : IMessageQueue
{
    public const string ActivitySourceName = $"{nameof(AkkaTracing)}.{nameof(TracingMailboxQueue)}";
    
    private readonly IMessageQueue _baseMailboxQueue;
    private readonly ConcurrentQueue<Activity?> _tellActivities;
    private Activity? _currentReceiveActivity;
    private readonly ActorTracing _tracing;

    public TracingMailboxQueue(IMessageQueue baseMailboxQueue, ActorTracing tracing)
    {
        _tracing = tracing;
        _baseMailboxQueue = baseMailboxQueue;
        _tellActivities = new ConcurrentQueue<Activity?>();
        _currentReceiveActivity = null;
    }

    public void Enqueue(IActorRef receiver, Envelope envelope)
    {
        if (!_tracing.TryGetCurrentActorSource(out var senderActivitySource))
        {
            senderActivitySource = _tracing.Get(envelope.Sender);
        }

        var messageType = GetMessageType(envelope.Message);
        var name = $"Tell {messageType.Name} To {receiver.Path}";

        Activity? tellActivity;
        if (envelope.Message is IScheduledTellMsg && Activity.Current is not null)
        {
            // Start a new trace, but link it to the timer creation
            var timerCreationContext = Activity.Current.Context;
            Activity.Current = null;
            tellActivity = senderActivitySource.StartActivity(ActivityKind.Producer, name: name, parentContext: default, links: new []{ new ActivityLink(timerCreationContext)});
        }
        else
        {
            tellActivity = senderActivitySource.StartActivity(ActivityKind.Producer, name: name);
        }

        
        tellActivity?.SetTag("message.type.name", messageType.Name);
        tellActivity?.SetTag("message.type.full-name", messageType);
        tellActivity?.SetTag("sender.path", envelope.Sender.Path);
        tellActivity?.SetTag("sender.uid", envelope.Sender.Path.Uid);
        tellActivity?.SetTag("receiver.path", receiver.Path);
        tellActivity?.SetTag("receiver.uid", receiver.Path.Uid);
        
        _baseMailboxQueue.Enqueue(receiver, envelope);
        
        tellActivity?.Stop();

        _tellActivities.Enqueue(tellActivity);
    }

    public bool TryDequeue(out Envelope envelope)
    {
        // We stop the receive activity associated with the PREVIOUS dequeued message when this is called - this is because
        // Akka will attempt to process all Mailbox messages in a batch by repeatedly calling TryDequeue until no
        // messages are left.
        _currentReceiveActivity?.Stop();
        
        var hasMessage = _baseMailboxQueue.TryDequeue(out envelope);
        if (!hasMessage) return false;
        
        _tellActivities.TryDequeue(out var tellActivity);

        var receiver = InternalCurrentActorCellKeeper.Current.Self;
        var source = _tracing.Get();

        var messageType = GetMessageType(envelope.Message);
        var name = $"Receive {messageType.Name} From {envelope.Sender.Path}";
        _currentReceiveActivity = tellActivity is null 
            ? source.StartActivity(name, ActivityKind.Consumer)
            : source.StartActivity(ActivityKind.Consumer, name: name, parentContext: tellActivity.Context);
        
        _currentReceiveActivity?.SetTag("message.type", messageType);
        _currentReceiveActivity?.SetTag("sender.path", envelope.Sender.Path);
        _currentReceiveActivity?.SetTag("sender.uid", envelope.Sender.Path.Uid);
        _currentReceiveActivity?.SetTag("receiver.path", receiver.Path);
        _currentReceiveActivity?.SetTag("receiver.uid", receiver.Path.Uid);
        return true;
    }

    private Type GetMessageType(object message)
    {
        if (message is IWrappedMessage wrappedMessage)
        {
            return wrappedMessage.Message.GetType();
        }
        else
        {
            return message.GetType();
        }
    }

    public void CleanUp(IActorRef owner, IMessageQueue deadletters)
    {
        _currentReceiveActivity?.Stop();

        _baseMailboxQueue.CleanUp(owner, deadletters);
    }

    public bool HasMessages => _baseMailboxQueue.HasMessages;
    public int Count => _baseMailboxQueue.Count;
}