# Contracts of a Minimalistic Actor System

This document supplements the Manifest. Contains signatures of all entities, their relationships, and clarifying comments. Implementation details are omitted.

---

## 1. Letter

```csharp
public abstract class Letter
{
    public Guid Sender { get; set; }
    public Guid Receiver { get; set; }

    protected Letter(Guid sender, Guid receiver);
}
```

Comment: Fields are mutable for the case when the same letter serves as a response — the sender swaps Sender/Receiver and sends it back (Manifest section 6: accumulator). All descendants are sealed (section 12).

---

## 2. System Letters

```csharp
public sealed class TimeServiceLetter : Letter
{
    public TimeoutCallback Callback { get; }
    public TimeServiceLetter(Guid sender, Guid receiver, TimeoutCallback callback);
}

public sealed class ShutdownLetter : Letter
{
    public ShutdownLetter(Guid sender, Guid receiver);
}

public sealed class InitializeLetter : Letter
{
    public InitializeLetter(Guid sender, Guid receiver);
}
```

Comment: TimeServiceLetter — timeout notification (section 22). Sender is SystemUids.TimeService. ShutdownLetter — request to terminate a specific actor. InitializeLetter — signal to the model actor to start building the model.

---

## 3. System Actor Identifiers

```csharp
public static class SystemUids
{
    public static readonly Guid System;
    public static readonly Guid TimeService;
    public static readonly Guid Model;
}
```

Comment: Static constant identifiers with fixed values. Accessible directly via SystemUids.System, etc. System — system identifier. TimeService — sender of TimeServiceLetter. Model — identifier of the model actor (ModelActor or CompiledModelActor).

---

## 4. Timeout and Time Service

```csharp
public readonly struct TimeoutCallback : IEquatable<TimeoutCallback>
{
    public Guid ActorUid { get; }
    public int CallbackId { get; }
    public Action Action { get; }
    public TimeoutCallback(Guid actorUid, int callbackId, Action action);
}

public interface ITimeService
{
    DateTime UtcNow { get; }

    // Registers a callback for the specified deadline.
    // If a callback with the same (ActorUid, CallbackId) is already registered,
    // the old registration is replaced with the new one.
    void Register(DateTime deadline, TimeoutCallback callback);
    void Register(TimeSpan timeout, TimeoutCallback callback);

    // Removes a callback registration. If not found, does nothing.
    void Unregister(TimeoutCallback callback);
}
```

Comment: TimeoutCallback is created by an actor once in its constructor. The same instance can be registered multiple times with different deadlines. CallbackId is unique within a single actor, assigned by the actor. The key in the timeout table is (ActorUid, CallbackId). Unregister completely removes the entry — both from the table and from the wait queue. TimeServiceLetter delivers the callback to the recipient actor; handling consists of calling callback.Action().

Implementations:
- SystemTimeService — production implementation with real system time, uses a background loop to track timeouts;
- VirtualTimeService — test implementation with virtual time, supports subscribers (IVirtualTimeSubscriber) for simulating the external environment.

---

## 5. Actor

```csharp
public abstract class Actor
{
    public Guid Uid { get; }
    public string Name { get; }
    public IActorSystem System { get; }
    public int QueueCapacity { get; }

    protected Actor(IActorSystem system, Guid uid, string name, int queueCapacity = DefaultQueueCapacity);
}
```

Internal members (accessible only to ActorSystem):

    internal bool TryEnqueue(Letter letter);
    internal async Task RunAsync(CancellationToken ct);
    internal void HandleSynchronously(Letter letter);

Protected members (overridden by descendants):

    protected abstract ValueTask OnLetter(Letter letter);
    protected virtual ValueTask OnShutdown();

Comment: Uid — universally unique identifier. Name — actor name for diagnostics. System — the only reference to the outside world, provides access to Logger, TimeService, Settings. The message queue is built into the actor (bounded, size set in constructor). When the queue overflows, messages are silently dropped.

Processing loop (RunAsync) sequentially reads letters from the queue and calls OnLetter. When CancellationToken is cancelled, OnShutdown is called, then the actor removes itself from the registry. Exceptions in OnLetter are logged and do not interrupt the loop.

HandleSynchronously is used in synchronous mode (TimeServiceModes.Sync) — calls OnLetter directly in the sender's thread, bypassing the queue. If OnLetter returns an incomplete Task, blocks the sender's thread until completion.

TryEnqueue attempts to place a letter in the queue. In TimeServiceModes.Async, calls System.IncrementActivity() to track pending activity.

---

## 6. Model Actor (Imperative Construction)

```csharp
public abstract class ModelActor : Actor
{
    // Always has Uid = SystemUids.Model, Name = "model"
    protected ModelActor(IActorSystem system, int queueCapacity = 256);

    // Sealed OnLetter — handles InitializeLetter, delegates the rest
    protected sealed override ValueTask OnLetter(Letter letter);

    // Called when InitializeLetter is received. Synchronous method.
    // Descendant creates application actors via Create().
    // On error, calls System.Panic().
    protected abstract void OnBuildModel();

    // Handles all letters except InitializeLetter.
    // By default, ignores (returns default).
    protected virtual ValueTask OnModelLetter(Letter letter);

    // Registers a child actor and immediately starts its processing loop.
    // Must be called only from OnBuildModel.
    protected Guid Create(Actor actor);
}
```

Comment: ModelActor is an intermediate link between system and application actors (Manifest section 15). Has constant Uid (SystemUids.Model) and name "model". OnLetter is sealed: when InitializeLetter is received, calls OnBuildModel; on error, logs and calls System.Panic(). All other letters are delegated to OnModelLetter.

Create registers a child actor in the system and immediately starts its RunAsync. The actor begins processing messages immediately after registration. Returns the Guid of the created actor.

---

## 7. Model Actor (Declarative Construction)

```csharp
public abstract class CompiledModelActor : Actor
{
    // Always has Uid = SystemUids.Model, Name = "model"
    protected CompiledModelActor(IActorSystem system, int queueCapacity = 256);

    // Sealed OnLetter — handles InitializeLetter, delegates the rest
    protected sealed override ValueTask OnLetter(Letter letter);

    // Compiles the model (from XML or another source)
    protected abstract CompiledModel CompileModel();

    // Creates an object from element configuration
    protected abstract object CreateObject(ElementConfig element);

    // Called after all objects are created, before actor registration.
    // Descendant establishes connections between objects.
    protected virtual void OnAfterCreate(CompiledModel model);

    // Handles all letters except InitializeLetter.
    // By default, ignores (returns default).
    protected virtual ValueTask OnModelLetter(Letter letter);

    // Retrieves a previously created object by identifier
    protected T GetObject<T>(Guid uid) where T : class;

    // Checks whether an object with the specified identifier was created
    protected bool HasObject(Guid uid);
}
```

Comment: CompiledModelActor is an alternative, declarative way to build the model. Also has Uid = SystemUids.Model and name "model". Build lifecycle: receive InitializeLetter → compile model via CompileModel() → create objects via CreateObject() for each element → call OnAfterCreate() to establish connections → register all actors in the system. On error at any stage, calls System.Panic().

A temporary dictionary _objects (Guid → object) stores all created objects until actor registration. After the model is assembled, actors are registered and begin work.

---

## 8. CompiledModel and Helper Classes

```csharp
public class CompiledModel
{
    public IReadOnlyCollection<Guid> Uids { get; }
    public int Count { get; }

    public void Add(ElementConfig element);
    public ElementConfig? FindElement(Guid uid);
    public IReadOnlyList<ElementConfig> FindByType(string elementType);
}

public class ElementConfig
{
    public Guid Uid { get; init; }
    public string ElementType { get; init; }
    public int PropertyCount { get; }
    public IReadOnlyCollection<string> PropertyNames { get; }

    public void AddProperty(string name, string value);
    public bool HasProperty(string name);
    public bool TryGetString(string name, out string value);
    public bool TryGetInt32(string name, out int value);
    public bool TryGetInt64(string name, out long value);
    public bool TryGetDouble(string name, out double value);
    public bool TryGetBoolean(string name, out bool value);
    public bool TryGetGuid(string name, out Guid value);
    public bool TryGetEnum<T>(string name, out T value) where T : struct, Enum;
}

public class ElementRule
{
    public static readonly ElementRule Default;

    public ElementRule WithProperty(string name);
    public ElementRule WithProperties(params string[] names);
    public ElementRule WithRequired(string name);
    public ElementRule WithRequired(params string[] names);
    public ElementRule WithGroupElement(string name);
    public ElementRule WithGroupElements(params string[] names);
    public bool IsProperty(string name);
    public bool IsRequired(string name);
    public bool IsGroupElement(string name);
    public IReadOnlyCollection<string> GetRequired();
}

public class XmlModelCompiler
{
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Errors { get; }

    public XmlModelCompiler(string uidAttributeName = "Uid");
    public XmlModelCompiler AddRule(string elementName, ElementRule? rule = null);
    public CompiledModel Compile(string xml);
}
```

Comment: CompiledModel — flat set of ElementConfig, the result of XML compilation. ElementConfig — description of one element (actor or auxiliary object). ElementRule — parsing rule for an XML element: which attributes are properties, which are required, which nested elements are grouping. XmlModelCompiler — configurable XML to CompiledModel compiler. Nested elements without attributes containing only text are saved as parent properties. Elements without Uid are skipped with a warning.

---

## 9. Actor Registry

```csharp
internal sealed class ActorRegistry
{
    public void Add(Actor actor);
    public void Remove(Guid uid);
    public bool TryGet(Guid uid, out Actor actor);
    public string GetName(Guid uid);
    public int Count { get; }
    public List<Actor> GetAll();
    public Task WaitForEmptyAsync();
}
```

Comment: Internal class, not accessible to external code (Manifest section 10). The only place in the system with direct references to Actor instances. Thread-safe (ConcurrentDictionary). GetAll() returns a snapshot. WaitForEmptyAsync completes when the registry becomes empty — used in WaitForShutdownAsync.

---

## 10. Actor System (Interface and Implementation)

```csharp
public interface IActorSystem
{
    void RegisterActor(Actor actor);
    void UnregisterActor(Guid uid);
    bool Send(Letter letter);
    void Shutdown();
    void Panic();
    Task WaitForShutdownAsync();
    CancellationToken CancellationToken { get; }
    bool IsPanic { get; }
    int ActorCount { get; }
    ILogger Logger { get; set; }
    ITimeService TimeService { get; set; }
    IActorSystemMetrics Metrics { get; set; }
    Settings Settings { get; }
    string GetActorName(Guid uid);
    List<Actor> GetAllActors();
    Actor? FindActor(Guid uid);

    // Internal methods for activity tracking (used in TimeServiceModes.Async)
    void IncrementActivity();
    void DecrementActivity();

    // Waits for all actors to finish processing current messages.
    // Used by VirtualTimeService for step synchronization.
    Task WaitAllIdleAsync();
}

public sealed class ActorSystem : IActorSystem
{
    public ActorSystem(Settings settings);

    public ILogger Logger { get; set; }
    public ITimeService TimeService { get; set; }
    public IActorSystemMetrics Metrics { get; set; }
    public Settings Settings { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsPanic { get; }
    public int ActorCount { get; }

    public void RegisterActor(Actor actor);
    public void UnregisterActor(Guid uid);
    public bool Send(Letter letter);
    public void Shutdown();
    public void Panic();
    public Task WaitForShutdownAsync();
    public string GetActorName(Guid uid);
    public List<Actor> GetAllActors();
    public Actor? FindActor(Guid uid);
}
```

Comment: Created with Settings. Logger and TimeService default to null implementations, replaced via public setters during build. Send() in TimeServiceModes.Sync processes the letter synchronously via HandleSynchronously, in other modes — via TryEnqueue.

RegisterActor adds an actor to the registry and immediately starts its RunAsync loop. No separate Start() method — actors begin work immediately after registration.

Shutdown() cancels CancellationToken. Panic() sets IsPanic = true and cancels CancellationToken. In TimeServiceModes.System, actors use CancellationToken for cooperative interruption of async operations (e.g., in SystemTimeService).

IncrementActivity/DecrementActivity methods are used in TimeServiceModes.Async to track the number of active operations. WaitAllIdleAsync waits for the activity counter to become zero — used in VirtualTimeService.StartVirtualClockAsync for virtual time step synchronization.

---

## 11. Settings

```csharp
public enum TimeServiceModes
{
    System,  // Production, real time, asynchronous sending
    Async,   // Tests with VirtualTimeService, asynchronous sending
    Sync     // Tests with VirtualTimeService, synchronous processing
}

public sealed class Settings
{
    public TimeServiceModes TimeServiceModes { get; init; } = TimeServiceModes.System;
}
```

Comment: Immutable after creation. TimeServiceModes determines how the system interacts with the time service and how letters are delivered. In Sync mode, letters are processed directly in the sender's thread (via HandleSynchronously), bypassing the queue. In System and Async modes, letters are queued and processed asynchronously.

---

## 12. Logger

Uses standard Microsoft.Extensions.Logging.ILogger. Actors access it via System.Logger.

Additional implementations:
- FileLogger — writes logs to a file with buffering (flush every 100ms);
- CallbackLogger — test implementation, passes messages to a delegate (convenient for checking logs in tests).

---

## 13. Metrics

```csharp
public interface IActorSystemMetrics
{
    void MessageSent();
    void MessageDropped();
    void ActorCreated();
    void ActorDestroyed();
    void ActorCountChanged(int count);
    Meter? Meter { get; }
}
```

Comment: Metrics are a replaceable object, similar to the logger and time service (Manifest section 24). Default is NullMetrics — all calls ignored, Meter returns null.

Methods called by the actor system:

- MessageSent() — when a letter is successfully queued to the recipient;
- MessageDropped() — when a letter is dropped due to queue overflow;
- ActorCreated() — when a new actor is registered in the registry;
- ActorDestroyed() — when an actor is removed from the registry;
- ActorCountChanged(int count) — when the number of actors in the registry changes.

The Meter property provides the standard Meter from System.Diagnostics.Metrics. Actors use it to create their own instruments (counters, histograms). If metrics are not configured, Meter returns null, and application metrics are not created.

Implementations:

- NullMetrics — default null implementation. All calls ignored, Meter = null. Thread-safe, singleton (NullMetrics.Instance);
- SystemMetrics — production implementation. Creates a Meter with name "MinimalActorSystem" (or accepts an existing one), registers counters messages.sent, messages.dropped, actors.created, actors.destroyed, and an observable gauge actors.active;
- LoggerMetrics (in MinimalActorSystem.Testing) — test implementation, outputs metrics to ILogger.

---

## 14. Virtual Time Subscriber

```csharp
public interface IVirtualTimeSubscriber
{
    string Name { get; }
    ValueTask OnTimeStep(DateTime currentTime);
}
```

Comment: Used only with VirtualTimeService. Subscribers are called at each virtual time step to simulate the external environment (telemetry sources, failure scenarios, etc.). Subscribe via VirtualTimeService.Subscribe(), unsubscribe via Unsubscribe().

---

## 15. Lifecycle

Build (synchronous phase):

    ActorSystem(settings)
    system.Logger = logger
    system.TimeService = timeService
    system.Metrics = new SystemMetrics()
    new ModelActor(system)              // or CompiledModelActor
    system.RegisterActor(modelActor)    // registers and starts RunAsync
    system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model))

Startup:

    Actors start immediately upon registration via RegisterActor.
    Model actor receives InitializeLetter:
      ModelActor:
        -> OnBuildModel()
          -> Create(actor1), Create(actor2), ...
      CompiledModelActor:
        -> CompileModel()
        -> CreateObject(element1), CreateObject(element2), ...
        -> OnAfterCreate(model)
        -> RegisterActor for all created actors
      -> system ready for operation

Normal operation (asynchronous):

    Actor waits for letters from the queue (ReadAsync, no thread blocking)
    When received — OnLetter(letter)
    Sending — System.Send(letter)

    In test mode (TimeServiceModes.Sync):
      Letters are processed synchronously in the sender's thread
      Actor does not read the queue — HandleSynchronously calls OnLetter directly

Actor termination:

    CancellationToken cancelled
    -> exit RunAsync loop
    -> OnShutdown()
    -> System.UnregisterActor(Uid)

System termination:

    ActorSystem.Shutdown() or Panic()
      -> _cts.Cancel()
      -> all actors terminate
      -> registry becomes empty
      -> WaitForShutdownAsync() completes
      -> external code checks system.IsPanic

Panic termination:

    Actor logs error, calls system.Panic()
    -> IsPanic = true
    -> _cts.Cancel()
    -> same as Shutdown from there