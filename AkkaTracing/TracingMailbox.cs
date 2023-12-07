using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Dispatch.MessageQueues;

namespace AkkaTracing;

public class TracingMailbox : MailboxType
{
    private readonly MailboxType _baseMailbox;

    public TracingMailbox(Settings settings, Config config) : base(settings, config)
    {
        _baseMailbox = new UnboundedMailbox(settings, config);
    }

    public override IMessageQueue Create(IActorRef owner, ActorSystem system)
    {
        return new TracingMailboxQueue(_baseMailbox.Create(owner, system));
    }
}