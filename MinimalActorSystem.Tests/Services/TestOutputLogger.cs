using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Tests;

public sealed class TestOutputLogger(ITestOutputHelper output) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}
