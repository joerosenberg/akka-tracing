using Akka.Configuration;
using Akka.Hosting;

namespace AkkaTracing.Hosting;

public static class AkkaTracingConfigurationExtensions
{
    public static AkkaConfigurationBuilder WithTracing(this AkkaConfigurationBuilder builder)
    {
        builder.WithActors((system, registry, di) =>
        {

        });

        return builder;
    }
}