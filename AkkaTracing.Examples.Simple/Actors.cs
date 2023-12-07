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
            Console.WriteLine(bars);
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