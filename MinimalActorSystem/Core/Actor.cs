using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace MinimalActorSystem;

public abstract class Actor
{
    private readonly Channel<Letter> _channel;

    public Guid Uid { get; }
    public string Name { get; }
    public IActorSystem System { get; }

    protected Actor(Guid uid, string name, IActorSystem system, int queueCapacity)
    {
        Uid = uid;
        Name = name;
        System = system;

        var options = new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<Letter>(options);
    }

    protected Actor(Guid uid, string name, IActorSystem system)
        : this(uid, name, system, system.Settings.DefaultQueueCapacity) { }

    [Conditional("TRACE_ACTORS")]
    protected void Trace(string message,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        var formatted = $"[{Thread.CurrentThread.ManagedThreadId}] {Path.GetFileName(file ?? "")}:{line} ({caller}) {Name}: {message}";
        System.Trace(formatted);
    }

    internal bool TryEnqueue(Letter letter)
    {
        bool success = _channel.Writer.TryWrite(letter);
        Trace(success
            ? $"Enqueued {letter.GetType().Name} from {System.GetActorName(letter.Sender)}"
            : $"Dropped {letter.GetType().Name} from {System.GetActorName(letter.Sender)} (queue full)");
        return success;
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        try
        {
            Trace("started");
            while (!ct.IsCancellationRequested)
            {
                var letter = await _channel.Reader.ReadAsync(ct);
                if (letter is ShutdownLetter)
                {
                    Trace($"Received ShutdownLetter from {System.GetActorName(letter.Sender)}");
                    break;
                }
                Trace($"Processing {letter.GetType().Name} from {System.GetActorName(letter.Sender)}");
                var task = OnLetter(letter);
                if (!task.IsCompletedSuccessfully)
                    await task;
            }
        }
        catch (OperationCanceledException)
        {
        }

        Trace("finished");
        await OnShutdown();
        System.UnregisterActor(Uid);
    }

    internal void HandleSynchronously(Letter letter)
    {
        Trace($"HandleSynchronously: {letter.GetType().Name}");
        try
        {
            var task = OnLetter(letter);
            if (!task.IsCompletedSuccessfully)
                task.AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Error in actor {Name}", System.GetActorName(Uid));
        }
    }

    protected abstract ValueTask OnLetter(Letter letter);
    protected virtual ValueTask OnShutdown() => default;
}
