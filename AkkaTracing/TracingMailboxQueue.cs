using System.Collections.Concurrent;
using System.Diagnostics;
using Akka.Actor;
using Akka.Actor.Internal;
using Akka.Dispatch.MessageQueues;

namespace AkkaTracing;

public class TracingMailboxQueue : IMessageQueue
{
    public const string ActivitySourceName = $"{nameof(AkkaTracing)}.{nameof(TracingMailboxQueue)}";
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    
    private readonly IMessageQueue _baseMailboxQueue;
    private readonly ConcurrentQueue<Activity?> _messageEnqueuedActivities;
    private Activity? _currentReceiveActivity;

    public TracingMailboxQueue(IMessageQueue baseMailboxQueue)
    {
        _baseMailboxQueue = baseMailboxQueue;
        _messageEnqueuedActivities = new ConcurrentQueue<Activity?>();
        _currentReceiveActivity = null;
    }
    
    public void Enqueue(IActorRef receiver, Envelope envelope)
    {
        using var tellActivity = ActivitySource.StartActivity("Tell", ActivityKind.Producer);
        tellActivity?.SetTag("sender.path", envelope.Sender.Path);
        tellActivity?.SetTag("receiver.path", receiver.Path);
        tellActivity?.SetTag("message.type", envelope.Message.GetType());
        
        _baseMailboxQueue.Enqueue(receiver, envelope);

        var enqueuedActivity = tellActivity is not null
            ? ActivitySource.StartActivity("Enqueued", ActivityKind.Internal, tellActivity.Context)
            : null;
        _messageEnqueuedActivities.Enqueue(enqueuedActivity);
    }

    public bool TryDequeue(out Envelope envelope)
    {
        // We stop the receive activity associated with the PREVIOUS dequeued message when this is called - this is because
        // Akka will attempt to process all Mailbox messages in a batch by repeatedly calling TryDequeue until no
        // messages are left.
        _currentReceiveActivity?.Stop();
        
        var hasMessage = _baseMailboxQueue.TryDequeue(out envelope);
        if (!hasMessage) return false;
        
        _messageEnqueuedActivities.TryDequeue(out var enqueuedActivity);
        _currentReceiveActivity = enqueuedActivity is not null
            ? ActivitySource.StartActivity("Receive", ActivityKind.Internal, enqueuedActivity.Context)
            : null;
        enqueuedActivity?.Stop();

        return true;
    }

    public void CleanUp(IActorRef owner, IMessageQueue deadletters)
    {
        _currentReceiveActivity?.Stop();
        while (_messageEnqueuedActivities.TryDequeue(out var enqueuedActivity))
        {
            enqueuedActivity?.Stop();
        }
        _baseMailboxQueue.CleanUp(owner, deadletters);
    }

    public bool HasMessages => _baseMailboxQueue.HasMessages;
    public int Count => _baseMailboxQueue.Count;
}