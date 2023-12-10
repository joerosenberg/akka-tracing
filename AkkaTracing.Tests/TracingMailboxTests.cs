using System.Diagnostics;
using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Configuration;
using Akka.TestKit.Configs;
using Akka.TestKit.Xunit;
using AkkaTracing.Mailbox;

namespace AkkaTracing.Tests;

public class TracingMailboxTests : TestKit
{
    private readonly ActivityListener _traceListener;

    private static readonly Config TracingConfig = ConfigurationFactory.ParseString("""
akka {
    actor {
        default-mailbox {
            mailbox-type: "AkkaTracing.TracingMailbox, AkkaTracing"
        }
    }
}
""");

    public TracingMailboxTests() : base(TracingConfig.WithFallback(TestConfigs.TestSchedulerConfig))
    {
        _traceListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TracingMailboxQueue.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_traceListener);
    }

    [Fact]
    public void Actor_Can_Be_Created_With_TracingMailbox()
    {
        ActorOf(Props.Empty);
    }
    
    [Fact]
    public async Task Message_Can_Be_Sent_Between_Actors_Created_With_TracingMailbox()
    {
        var (receiverProps, receivedTcs) = SingleReceiverActor<string>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell("Hello!", sender);

        var received = await receivedTcs.Task;
        Assert.Equal("Hello!", received);
    }

    [Fact]
    public async Task Trace_Started_For_Message_Between_Actors()
    {
        var traceStartedTcs = new TaskCompletionSource();
        _traceListener.ActivityStarted = activity => traceStartedTcs.SetResult();
        
        var (receiverProps, receivedTcs) = SingleReceiverActor<string>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell("Hello!", sender);

        await receivedTcs.Task;
        await traceStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task Trace_Stopped_For_Message_Between_Actors()
    {
        var traceStoppedTcs = new TaskCompletionSource();
        _traceListener.ActivityStopped = activity => traceStoppedTcs.SetResult();
        
        var (receiverProps, receivedTcs) = SingleReceiverActor<string>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell("Hello!", sender);

        await receivedTcs.Task;
        await traceStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public async Task Message_Trace_Has_Sender_Path()
    {
        var traceStoppedTcs = new TaskCompletionSource<Activity>();
        _traceListener.ActivityStopped = activity => traceStoppedTcs.SetResult(activity);
        
        var (receiverProps, receivedTcs) = SingleReceiverActor<string>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell("Hello!", sender);

        await receivedTcs.Task;
        var activity = await traceStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(sender.Path, (ActorPath)activity.GetTagItem("sender.path")!);
    }
    
    [Fact]
    public async Task Message_Trace_Has_Receiver_Path()
    {
        var traceStoppedTcs = new TaskCompletionSource<Activity>();
        _traceListener.ActivityStopped = activity => traceStoppedTcs.SetResult(activity);
        
        var (receiverProps, receivedTcs) = SingleReceiverActor<string>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell("Hello!", sender);

        await receivedTcs.Task;
        var activity = await traceStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(receiver.Path, (ActorPath)activity.GetTagItem("receiver.path")!);
    }

    [Fact]
    public async Task Message_Trace_Has_Message_Type()
    {
        var traceStoppedTcs = new TaskCompletionSource<Activity>();
        _traceListener.ActivityStopped = activity => traceStoppedTcs.SetResult(activity);
        
        var (receiverProps, receivedTcs) = SingleReceiverActor<int>.Props();

        var sender = ActorOf(SenderActor.Props());
        var receiver = ActorOf(receiverProps);
        
        receiver.Tell(3, sender);

        await receivedTcs.Task;
        var activity = await traceStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(typeof(int), (Type)activity.GetTagItem("message.type")!);
    }

    [Fact]
    public async Task Message_Trace_Span_Ends_After_Receiver_Finishes_Handling_Message()
    {
        var messageHandledTcs = new TaskCompletionSource();
        var traceEndedAfterMessageHandledTcs = new TaskCompletionSource<bool>();
        _traceListener.ActivityStopped = _ => traceEndedAfterMessageHandledTcs.SetResult(messageHandledTcs.Task.IsCompletedSuccessfully);

        Action<IActorDsl> receiverConfig = act =>
        {
            act.ReceiveAsync<string>(async (s, context) =>
            {
                await Task.Delay(500);
                messageHandledTcs.SetResult();
            });
        };
        var receiverProps = Props.Create(() => new Act(receiverConfig));
        var receiver = ActorOf(receiverProps);

        receiver.Tell("DoStuff");

        var messageHandled = await traceEndedAfterMessageHandledTcs.Task;
        Assert.True(messageHandled);
    }
    
    [Fact]
    public async Task Trace_Spans_For_Sequential_Messages_Start_And_End_In_Order()
    {
        var tracesInOrderTcs = new TaskCompletionSource<bool>();
        var stage = "start"; 
        _traceListener.ActivityStarted = activity =>
        {
            var state = (stage, (Type)activity.GetTagItem("message.type")!);
            if (state == ("start", typeof(string)))
            {
                stage = "processing-string";
            }
            else if (state == ("processed-string", typeof(int)))
            {
                stage = "processing-int";
            }
            else
            {
                tracesInOrderTcs.SetResult(false);
            }
        };

        _traceListener.ActivityStopped = activity =>
        {
            var state = (stage, (Type)activity.GetTagItem("message.type")!);
            if (state == ("processing-string", typeof(string)))
            {
                stage = "processed-string";
            }
            else if (state == ("processing-int", typeof(int)))
            {
                stage = "processed-int";
                tracesInOrderTcs.SetResult(true);
            }
            else
            {
                tracesInOrderTcs.SetResult(false);
            }
        };

        Action<IActorDsl> receiverConfig = act =>
        {
            act.ReceiveAsync<string>(async (s, context) =>
            {
                await Task.Delay(500);
            });
            act.ReceiveAsync<int>(async (i, context) =>
            {
                await Task.Delay(500);
            });
        };
        var receiverProps = Props.Create(() => new Act(receiverConfig));
        var receiver = ActorOf(receiverProps);

        receiver.Tell("DoStuff");
        receiver.Tell(123);

        var tracesInOrder = await tracesInOrderTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(tracesInOrder);
    }

    private class SingleReceiverActor<T> : ReceiveActor
    {
        public static (Props Props, TaskCompletionSource<T> ReceivedTcs) Props()
        {
            var receivedTcs = new TaskCompletionSource<T>();
            var props = Akka.Actor.Props.Create(() => new SingleReceiverActor<T>(receivedTcs));
            return (props, receivedTcs);
        }
        
        public SingleReceiverActor(TaskCompletionSource<T> receivedTcs)
        {
            Receive<T>(receivedTcs.SetResult);
        }
    }

    public override void Shutdown(TimeSpan? duration = null, bool verifySystemShutdown = false)
    {
        _traceListener.Dispose();
        base.Shutdown(duration, true);
    }

    private class SenderActor : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new SenderActor());
    }
}