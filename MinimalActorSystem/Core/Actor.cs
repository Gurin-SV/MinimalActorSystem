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

    internal bool TryEnqueue(Letter letter)
    {
        return _channel.Writer.TryWrite(letter);
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var letter = await _channel.Reader.ReadAsync(ct);
                await OnLetter(letter);
            }
        }
        catch (OperationCanceledException) { }

        await OnShutdown();
        System.UnregisterActor(Uid);
    }

    internal void HandleSynchronously(Letter letter)
    {
        try
        {
            OnLetter(letter).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Ошибка в акторе {Uid}", Uid);
        }
    }

    protected abstract Task OnLetter(Letter letter);
    protected virtual Task OnShutdown() => Task.CompletedTask;
}
