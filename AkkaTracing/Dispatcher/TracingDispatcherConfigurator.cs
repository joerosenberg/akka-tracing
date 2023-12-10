using Akka.Configuration;
using Akka.Dispatch;

namespace AkkaTracing.Dispatcher;

public class TracingDispatcherConfigurator : MessageDispatcherConfigurator
{
    private readonly TracingDispatcher _instance;
    
    public TracingDispatcherConfigurator(Config config, IDispatcherPrerequisites prerequisites) : base(config, prerequisites)
    {
        TimeSpan deadlineTime = Config.GetTimeSpan("throughput-deadline-time", null);
        long? deadlineTimeTicks = null;
        if (deadlineTime.Ticks > 0)
            deadlineTimeTicks = deadlineTime.Ticks;

        if (Config.IsNullOrEmpty())
            throw ConfigurationException.NullOrEmptyConfig<DispatcherConfigurator>();

        _instance = new TracingDispatcher(this, Config.GetString("id"),
            Config.GetInt("throughput"),
            deadlineTimeTicks,
            ConfigureExecutor(),
            Config.GetTimeSpan("shutdown-timeout"));
    }

    public override MessageDispatcher Dispatcher()
    {
        return _instance;
    }
}