using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Dispatch.MessageQueues;
using AkkaTracing.Extension;

namespace AkkaTracing.Mailbox;

internal sealed class TracingMailbox : MailboxType
{
    private readonly MailboxType _baseMailbox;

    public TracingMailbox(Settings settings, Config config) : base(settings, config)
    {
        _baseMailbox = new UnboundedMailbox(settings, config);
    }

    public override IMessageQueue Create(IActorRef owner, ActorSystem system)
    {
        var tracing = system.WithExtension<ActorTracing, ActorTracingExtension>();

        return new TracingMailboxQueue(_baseMailbox.Create(owner, system), tracing);
    }
}