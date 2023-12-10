using Akka.Actor;

namespace AkkaTracing.Utils;

internal static class ActorRefExtensions
{
    public static string GetShortActorTypeName(this IActorRef actorRef)
    {
        return GetActorType(actorRef).Name;
    }
    
    public static Type GetActorType(this IActorRef actorRef)
    {
        return actorRef switch
        {
            ActorRefWithCell refWithCell => refWithCell.Underlying.Props.Type,
            MinimalActorRef minimalRef => minimalRef.GetType(),
            _ => actorRef.GetType()
        };
    }
}