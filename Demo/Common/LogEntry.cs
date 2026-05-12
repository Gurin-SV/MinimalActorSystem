namespace Demo.Common;

public sealed class LogEntry(string message)
{
    public string Message { get; } = message;

    public override string ToString() => Message;
}
