using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Tests;

public static class SystemFactory
{
    public static ActorSystem CreateSystem(ITestOutputHelper output, Settings? settings = null,
        LogLevel minLevel = LogLevel.Trace)
    {
        var system = new ActorSystem(settings ?? new Settings { IsProduction = false });
        system.CreateTestLogger(line => output.WriteLine(line), minLevel);
        return system;
    }

    public static ActorSystem CreateSystem(Settings? settings = null)
    {
        return new ActorSystem(settings ?? new Settings { IsProduction = false });
    }
}
