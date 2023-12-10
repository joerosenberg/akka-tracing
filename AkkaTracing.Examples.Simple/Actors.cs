using Akka.Actor;
using Akka.Hosting;

namespace AkkaTracing.Examples.Simple;

public class FooActor : ReceiveActor, IWithTimers
{
    public FooActor(IRequiredActor<BarActor> barActor)
    {
        Receive<string>(s => s == "foo", _ =>
        {
            barActor.ActorRef.Tell("bar");
        });
        Receive<int>(bars =>
        {
            var jobId = $"FooBarJob-{bars}";
            Context.ActorOf<FooBarJob>(jobId);
            Context.Child(jobId).Tell(new FooBarJob.Initialize(bars));
        });
        Receive<FooBarJob.Complete>(msg =>
        {
            Sender.Tell(PoisonPill.Instance);
        });
    }

    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("foo", "foo", TimeSpan.FromSeconds(10));
        base.PreStart();
    }

    public ITimerScheduler Timers { get; set; }
}

public class BarActor : ReceiveActor
{
    private int _bars;
    
    public BarActor()
    {
        Receive<string>(s => s == "bar", _ =>
        {
            _bars++;
            Sender.Tell(_bars);
        });
    }
}

public class FooBarJob : ReceiveActor
{
    public record Initialize(int Bars);

    public record Complete(int Result);

    public FooBarJob()
    {
        Receive<Initialize>(msg =>
        {
            Sender.Tell(new Complete(msg.Bars * msg.Bars));
        });
    }
}