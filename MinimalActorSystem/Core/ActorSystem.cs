using System.Diagnostics;

namespace MinimalActorSystem;

public interface IActorSystem
{
    Settings Settings { get; }
    ILogger Logger { get; }
    ITimeService TimeService { get; }
    CancellationToken CancellationToken { get; }
    SystemUids Uids { get; }
    bool IsPanic { get; }

    void SetLogger(ILogger logger);
    void SetTimeService(ITimeService timeService);
    void RegisterActor(Actor actor);
    void UnregisterActor(Guid uid);
    void Send(Letter letter);
    void Start();
    void Shutdown();
    void Panic();
    Task WaitForShutdownAsync();
    string GetActorName(Guid uid);
    List<Actor> GetAllActors();
    Actor? FindActor(Guid uid);
    void Trace(string message);
}

public sealed class ActorSystem : IActorSystem
{
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    private sealed class NullTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public void Register(DateTime deadline, TimeoutCallback callback) { }
        public void Register(TimeSpan timeout, TimeoutCallback callback) { }
        public void Unregister(TimeoutCallback callback) { }
    }

    private readonly CancellationTokenSource _cts;
    private readonly ActorRegistry _registry;
    private volatile bool _isPanic;
    private volatile bool _started;

    public SystemUids Uids { get; }
    public ILogger Logger { get; private set; }
    public ITimeService TimeService { get; private set; }
    public Settings Settings { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsPanic => _isPanic;

    internal int ActorCount => _registry.Count;

    public ActorSystem(Settings settings)
    {
        Settings = settings;
        _cts = new();
        CancellationToken = _cts.Token;
        _registry = new(this);
        Uids = new();
        Logger = new NullLogger();
        TimeService = new NullTimeService();
    }

    public void SetLogger(ILogger logger)
    {
        Logger = logger;
    }

    public void SetTimeService(ITimeService timeService)
    {
        TimeService = timeService;
    }

    void IActorSystem.Trace(string message)
    {
        Logger.Log(LogLevel.Trace, "{Message}", message);
    }

    [Conditional("TRACE_ACTORS")]
    private void Trace(string message)
    {
        Logger.Log(LogLevel.Trace, "{Message}", message);
    }

    public void RegisterActor(Actor actor)
    {
        Trace(actor.Name);
        _registry.Add(actor);
        if (_started)
            _ = actor.RunAsync(CancellationToken);
    }

    public void UnregisterActor(Guid uid)
    {
        Trace(GetActorName(uid));
        _registry.Remove(uid);
    }

    public void Send(Letter letter)
    {
        Trace($"Send {letter.GetType().Name} from {GetActorName(letter.Sender)} to {GetActorName(letter.Receiver)}");
        if (_registry.TryGet(letter.Receiver, out var actor))
        {
            if (Settings.SynchronousProcessing)
                actor.HandleSynchronously(letter);
            else
                actor.TryEnqueue(letter);
        }
    }

    public void Start()
    {
        Trace("Start");
        _started = true;
        foreach (var actor in _registry.GetAll())
        {
            _ = actor.RunAsync(CancellationToken);
        }
    }

    public void Shutdown()
    {
        Trace("Shutdown");
        _cts.Cancel();
    }

    public void Panic()
    {
        Trace("Panic");
        _isPanic = true;
        _cts.Cancel();
    }

    public Task WaitForShutdownAsync()
    {
        Trace("WaitForShutdownAsync");
        return _registry.WaitForEmptyAsync();
    }

    public string GetActorName(Guid uid)
    {
        return _registry.GetName(uid);
    }

    public List<Actor> GetAllActors()
    {
        return _registry.GetAll();
    }

    public Actor? FindActor(Guid uid)
    {
        if (_registry.TryGet(uid, out var actor))
            return actor;
        return null;
    }
}
