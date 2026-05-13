# Manifest of a Minimalistic Actor System

**1. Pure minimalism. Nothing superfluous.**
Every element of the system must have an unambiguous reason for existence.
If functionality can be placed outside the core — it is placed outside.

**2. Manifest as foundation and memory.**
All subsequent points serve two purposes:
- serve as a foundation for writing code (architectural decisions are derived from the manifesto points);
- restore context after context overflow (just read the manifesto to continue development).

**3. Execution environment**
Programming language — C#, target platform — .NET Core. All implementation decisions must leverage the capabilities of this ecosystem (including System.Threading.Channels, Task, Span, platform hardware features), but without unnecessary dependencies.

**4. Isolation and interaction**
Actors are parallel by nature and completely independent of each other. The only way to interact between actors is asynchronous message passing. No shared mutable state, no direct synchronous calls between actors.

**5. Incoming message queue**
Each actor has an incoming message queue. Messages are processed strictly sequentially — in the order they arrived in the queue. No concurrent processing inside a single actor instance: at any moment, an actor processes exactly one message.

**6. Nature of a message**
A message is an object passed between actors. It can be:
- a one-way request;
- a response to a previous message;
- an accumulator that mutates as it passes sequentially from actor to actor (movable mutable message).

**7. Actor lightweightness**
The maximum number of actors in the system is limited only by available resources (memory, CPU). No architectural limits on the number of instances.

**8. Actor activity**
An actor is active only when processing a message from its queue. At all other times, it consumes no CPU time, holds no thread, and exhibits no background activity. It follows that: an actor is essentially state + a message queue. Behavior is a function that runs when a message arrives and then immediately sleeps.

**9. Actor identification**
Each actor has a unique identifier — Guid. The identifier is assigned when the actor is created and never changes throughout its lifecycle.

**10. Actor registry**
The actor system contains an internal dictionary Guid → Actor. This is the only place in the system that holds a direct reference to an actor instance. External code does not have direct access to this dictionary.

**11. Send method**
Sending a message to an actor is done through the actor system's public method:

    Send(letter: Letter)

This is the only way to deliver a message to an actor. The method finds the actor in the registry by identifier and places the message in its queue.

**12. Message typing**
Messages are strictly typed. A message type is always a sealed class, inheritance of messages is prohibited. The purpose, structure, and semantics of a message are entirely determined by its type. All messages inherit from the abstract class Letter, which contains two fields: Sender (Guid of sender) and Receiver (Guid of receiver). Message classes themselves are sealed, inheritance of semantics is prohibited.

**13. Actor system instance**
The actor system is an instance object. No static references, global state, or singletons. Multiple independent actor systems may exist within a single application or service.

**14. Actor access to the system**
A reference to the actor system is passed to the actor through its constructor upon creation. This is the only way for an actor to gain the ability to send messages to other actors.

**15. Actor creation**
The initial set of actors that form connections of the actor system with external services (I/O, HTTP, queues, etc.) is created during actor system initialization. One of these actors is the root parent and serves as a factory for the application's actors, creating the first layer of application actors.

Application actors are created dynamically — the parent spawns a child through the actor system and receives its identifier (Guid). The child can, in turn, create its own children, forming a tree of actors.

System actors have constant identifiers (Guid) accessible through the static class SystemUids. This allows application actors to send messages to system actors directly, without needing to find them by type or name. System actors are independent of any specific application program and application actors, but the set of messages each system actor can receive is fixed. System actors cannot send messages to application actors except in response messages.

The application actor parent is an intermediate link between system actors and application actors. It has a constant identifier (SystemUids.Model). Two approaches to model construction are supported:

- **Imperative** — by inheriting from `ModelActor`. The descendant overrides `OnBuildModel()` and creates application actors via the `Create()` method. `ModelActor` receives `InitializeLetter`, processes it in sealed override `OnLetter`, and calls `OnBuildModel`. On error in `OnBuildModel`, it calls the actor system's `Panic`. All other letters are delegated to the virtual method `OnModelLetter`.

- **Declarative** — by inheriting from `CompiledModelActor`. The descendant implements `CompileModel()` to compile an XML description into `CompiledModel`, and `CreateObject()` to create objects from `ElementConfig`. `CompiledModelActor` also has `Uid = SystemUids.Model` and follows the same lifecycle: receives `InitializeLetter`, compiles the model, creates objects, establishes connections via `OnAfterCreate()`, and registers actors. On error at any stage, it calls `Panic`.

Only one model actor (of any type) may exist in a single actor system.

**16. Logging**
The logger is a simple object (not an actor), accessible through the actor system property. All actors call logger methods directly, without sending messages. The choice of a simple object is made for efficiency: logging should not go through message queues and actor threads.

This is an important clarification: not everything in the system has to be an actor. Utility cross-cutting concerns remain objects.

**17. Error handling**
Errors do not escape the actor. Each actor handles exceptions that occur during message processing internally. If a message cannot be processed, the actor logs details and continues working with the next message.

**18. Panic shutdown**
If an error makes further operation of the actor impossible, the actor logs the reason (CriticalError) and calls the actor system's Panic method. The actor system sets the IsPanic flag and cancels the CancellationToken.

Each actor, upon detecting token cancellation:
- terminates its message processing loop;
- calls OnShutdown to properly release resources;
- removes itself from the registry.

When the actor registry becomes empty, WaitForShutdownAsync completes. External code checks IsPanic to determine the return code (0 — normal shutdown, 1 — panic shutdown).

**19. Ignoring unknown messages**
If an actor receives a message of a type it does not recognize, it ignores it (with optional logging). An actor can accept any message, but only processes those for which it has a defined handler.

This allows actor versioning: older implementations may not know about new message types and continue working stably, while newer implementations handle an extended set. The system evolves smoothly, without requiring all actors to be updated simultaneously. An actor is an elastic handler: unknown messages don't break it, new behavior is added gradually.

**20. Actor lifecycle**
The actor lifecycle consists of three phases:

- **Creation.** In the constructor, the actor initializes all its internal structures and message queue. After creation, the actor registers in the registry and immediately starts its message processing loop. The actor is ready to receive messages immediately after registration.

- **Active phase.** During most of its life, the actor waits for messages from its queue and processes them sequentially.

- **Termination.** Upon detecting CancellationToken cancellation (during Shutdown or Panic), the actor:
  - exits the message processing loop;
  - calls OnShutdown, where it carefully terminates state and releases resources;
  - calls UnregisterActor, removing itself from the registry.

After termination, the actor cannot become active again.

**21. Shutdown waiting and cooperative cancellation**
The actor system provides a public CancellationToken property. The token transitions to Cancelled state after calling Panic (panic shutdown) or Shutdown (normal shutdown). Actors use this token for cooperative interruption of internal async operations during system shutdown.

The IsPanic property allows external code to determine the reason for termination: false for Shutdown, true for Panic.

The WaitForShutdownAsync method asynchronously waits for the actor registry to become empty.

**22. TimeService**
The actor system provides a time service — a simple object (not an actor), accessible to actors through the actor system property.

The time service provides:
- current UTC time — the only way for actors to get time;
- methods for registering and canceling timeouts, used by actors to implement timeout operations.

Purposes of the time service:

**Time substitution in tests.** In production, the service provides real system time (`SystemTimeService`). In test mode — virtual time (`VirtualTimeService`), allowing long intervals to execute instantly. This provides fast and deterministic unit tests without real delays.

**Universal timeout error criterion.** Actors use the time service to track timeouts (e.g., during reconnection). If an action is not completed within a given virtual time interval, it is considered failed and requires either retry or other reaction.

**Operation modes.** The `Settings.TimeServiceModes` setting determines how the system interacts with the time service:
- `System` — production mode with `SystemTimeService`, messages are queued asynchronously;
- `Async` — test mode with `VirtualTimeService` and asynchronous processing, activity tracking via `IncrementActivity`/`DecrementActivity`;
- `Sync` — test mode with `VirtualTimeService` and synchronous message processing directly in the sender's thread.

**23. System settings**
The actor system contains a settings object — a simple object (not an actor), accessible to actors through the actor system property.

Purpose of the settings object:
- store configuration parameters for the actor system and actors;
- provide actors with a single access point to settings without directly accessing static classes, environment variables, or configuration files.

The settings object:
- is immutable after actor system initialization — changes during operation are not allowed;
- contains the required field `TimeServiceModes` (time service operation mode: `System`, `Async`, or `Sync`);
- may contain additional parameters: timeouts, limits, mode flags, logging levels, queue buffer sizes, etc.;
- in test environments, can be replaced with a specialized test configuration.

**24. Metrics**

The actor system provides metrics — a simple object (not an actor), accessible to actors through the actor system property.

Purpose of metrics:

- provide system counters: number of messages sent and dropped, actors created and destroyed, current registry size;
- provide actors with a standard `Meter` (`System.Diagnostics.Metrics`) for creating application metrics without additional dependencies.

Metrics follow the same principles as the logger and time service:

- replaceable implementation via `IActorSystemMetrics` interface;
- default null implementation — all calls ignored, zero overhead;
- production implementation (`SystemMetrics`) uses standard `System.Diagnostics.Metrics` and integrates with OpenTelemetry, Prometheus, and other collectors;
- test implementation (`LoggerMetrics` in `MinimalActorSystem.Testing`) outputs metrics to the log;
- application actors can create their own instruments via `System.Metrics.Meter`, independent of the specific implementation.