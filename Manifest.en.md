# Manifesto of the Minimalist Actor System

**1. Absolute minimalism. Nothing superfluous.**
Every element of the system must have an unambiguous reason for existence.
If functionality can be moved outside the core, it is moved.

**2. Manifesto as foundation and memory.**
All subsequent clauses serve two purposes:
- to serve as the basis for writing code (architectural decisions are derived from the clauses of the manifesto);
- to restore context after it overflows (reading the manifesto is enough to continue development).

**3. Execution environment**
The programming language is C#, the target platform is .NET. All implementation decisions must leverage the capabilities of this ecosystem (including System.Threading.Channels, Task, Span, hardware platform features), but without unnecessary dependencies.

**4. Isolation and interaction**
Actors are parallel by nature and completely independent of each other. The only way for actors to interact is through asynchronous message exchange. No shared mutable state, no direct synchronous calls between actors.

**5. Incoming message queue**
Each actor has an incoming message queue. Messages are processed by the actor strictly sequentially — in the order they arrived in the queue. No concurrent processing within a single actor instance: at any given moment, the actor processes exactly one message.

**6. Nature of a message**
A message is an object passed between actors. It can be:
- a one-way request;
- a reply to a previous message;
- an accumulator that mutates when passed sequentially from actor to actor (a movable mutable message).

**7. Lightweight actors**
The maximum number of actors in the system is limited solely by available resources (memory, CPU). There are no architectural limits on the number of instances.

**8. Actor activity**
An actor is active only when processing a message from its queue. The rest of the time it consumes no CPU time, holds no thread, and exhibits no background activity. It follows that an actor is essentially state + a message queue. Behavior is a function that executes when a message appears and then immediately goes to sleep.

**9. Actor identification**
Each actor has a unique identifier — a Guid. The identifier is assigned at actor creation and does not change throughout its lifecycle.

**10. Actor registry**
The actor system contains an internal Guid → Actor dictionary. This is the only place in the system where a direct reference to an actor instance is stored. External code has no direct access to this dictionary.

**11. Send method**
Sending a message to an actor is performed through the public method of the actor system:

    Send(letter: Letter)

This is the only way to deliver a message to an actor. The method locates the actor in the registry by its identifier and places the message in its queue.

**12. Message typing**
Messages are strictly typed. A message type is always a final class (sealed); message inheritance is prohibited. The purpose, structure, and semantics of a message are fully determined by its type. All messages inherit from the abstract Letter class, which contains two fields: Sender (the sender's Guid) and Receiver (the receiver's Guid). Message classes themselves are sealed; semantic inheritance is prohibited.

**13. Actor system instances**
The actor system is an instance object. No static references, global state, or singletons. Multiple independent actor systems are allowed within a single application or service.

**14. Actor access to the system**
A reference to the actor system is passed to the actor through its constructor upon creation. This is the only way for an actor to gain the ability to send messages to other actors.

**15. Actor creation**
The initial set of actors that form the connections of the actor system to external services (I/O, HTTP, queues, etc.) is created during the initialization of the actor system. One of these actors is the root parent and acts as a factory for application task actors, creating the first layer of application actors.

Application actors are created dynamically — the parent spawns a child through the actor system and receives its identifier (Guid). The child, in turn, can create its own children, forming an actor tree.

System actors have constant identifiers (Guid), accessible through the static SystemUids class. This allows application actors to send messages to system actors directly, without needing to look them up by type or name. System actors are independent of any specific application program and application actors, but for each system actor, the set of messages it can accept is fixed. System actors cannot send messages to application actors except in the case of a reply message.

The parent actor of application actors is an intermediary between system and application actors. It has a constant identifier (SystemUids.Model). Model construction is performed imperatively — by inheriting from ModelActor. The descendant overrides OnBuildModel() and creates application actors through the Create() method. ModelActor receives InitializeLetter, processes it, and calls OnBuildModel. On error in OnBuildModel, it calls the actor system's Panic.

Only one model actor can exist in a single actor system.

**16. Logging**
The logger is a simple object (not an actor), accessible through a property of the actor system. All actors call the logger's methods directly, without sending messages. The choice in favor of a simple object was made for efficiency reasons: logging should not go through the message queue and the actor's thread.

This is an important clarification: not everything in the system must be an actor. Utility cross-cutting concerns remain objects.

**17. Error handling**
Errors do not leave the actor's boundaries. Each actor handles exceptions that occur during message processing internally. If a message cannot be processed, the actor outputs details to the log and continues working with the next message.

**18. Panic**
If an error makes the further operation of the actor impossible, the actor outputs the reason to the log (CriticalError) and calls the Panic method of the actor system. The actor system sets the IsPanic flag and cancels the CancellationToken.

Each actor, upon detecting the token cancellation:
- exits the message processing loop;
- calls OnShutdown for proper resource release;
- removes itself from the registry.

When the actor registry becomes empty, WaitForShutdownAsync completes. External code checks IsPanic to determine the exit code (0 — normal shutdown, 1 — panic).

**19. Ignoring unknown messages**
If an actor receives a message of a type unknown to it, it ignores it (with possible logging). An actor can accept any messages but processes only those for which it has a handler defined.

This enables actor versioning: old implementations may not know about new message types and continue stable operation, while new implementations handle an extended set. The system evolves smoothly, without the need to update all actors simultaneously. An actor is an elastic handler: the unknown does not break it, the new is added gradually.

**20. Actor lifecycle**
The actor lifecycle consists of three phases:

- Creation. In its constructor, the actor initializes all its internal structures and the message queue. After creation, the actor registers in the registry and immediately starts the message processing loop. The actor is ready to receive messages immediately after registration.
- Active phase. For the main part of its life, the actor waits for messages from its queue and processes them sequentially.
- Shutdown. Upon detecting the CancellationToken cancellation (during Shutdown or Panic), the actor exits the message processing loop, calls OnShutdown, where it cleanly finalizes state and releases resources, then calls UnregisterActor, removing itself from the registry.

After shutdown, the actor cannot become active again.

**21. Shutdown await and cooperative cancellation**
The actor system provides a public CancellationToken property. The token transitions to the Cancelled state after calling Panic (panic) or Shutdown (normal shutdown). Actors use this token for cooperative cancellation of internal asynchronous operations when the system is stopping.

The IsPanic property allows external code to determine the reason for shutdown: false for Shutdown, true for Panic.

The WaitForShutdownAsync method asynchronously waits for the actor registry to empty.

**22. Time service**
The actor system provides a time service — a simple object (not an actor), accessible to actors through a public property of the actor system.

The time service provides:
- the current time in UTC — the only way for actors to get time;
- methods for registering and canceling timeouts, used by actors to implement timeout-based operations.

Purposes of the time service:

Time substitution in tests. In production, the service provides real system time (SystemTimeService). In test mode, virtual time (VirtualTimeService) is used, allowing long intervals to execute instantly. This ensures fast and deterministic unit tests without real delays.

Universal timeout error criterion. Actors use the time service to track timeouts (for example, during reconnection). If an action is not completed within a given virtual time interval, it is considered unfulfilled and requires either a retry or another reaction.

Operating modes. The Settings.TimeServiceModes setting determines how the system interacts with the time service:
- System — production mode with SystemTimeService, messages are placed in the queue asynchronously;
- Async — test mode with VirtualTimeService and asynchronous processing, activity is tracked via IncrementActivity/DecrementActivity;
- Sync — test mode with VirtualTimeService and synchronous message processing directly in the sender's thread.

**23. System settings**
The actor system contains a settings object — a simple object (not an actor), accessible to actors through a public property of the actor system.

Purpose of the settings object:
- to store configuration parameters of the actor system and actors;
- to provide actors with a single point of access to settings without directly accessing static classes, environment variables, or configuration files.

The settings object:
- is immutable after the actor system initialization — changes to settings during operation are not allowed;
- contains a mandatory TimeServiceModes field (time service operating mode: System, Async, or Sync);
- can contain additional parameters: timeouts, limits, mode flags, logging levels, queue buffer sizes, etc.;
- in a test environment, can be replaced with a specialized test configuration.

**24. Metrics**
The actor system provides metrics — a simple object (not an actor), accessible to actors through a public property of the actor system.

Purpose of metrics:
- to provide system counters: the number of sent and dropped messages, created and destroyed actors, the current size of the registry;
- to provide actors with a standard Meter (System.Diagnostics.Metrics) for creating application-level metrics without additional dependencies.

Metrics follow the same principles as the logger and time service:
- replaceable implementation via the IActorSystemMetrics interface;
- null implementation by default — all calls are ignored, zero overhead;
- the production implementation (SystemMetrics) uses the standard System.Diagnostics.Metrics and integrates with OpenTelemetry, Prometheus, and other collectors;
- application actors can create their own instruments through System.Metrics.Meter, independent of the specific implementation.

**25. Automatic message dispatching (Source Generator)**
The actor system includes the ActorLetterHandlerGenerator source generator, which eliminates the need to manually write the OnLetter method with a switch. The generator is located in the separate MinimalActorSystem.SourceGenerator assembly and adds no runtime dependencies.

How it works:
- The actor is marked with the [ActorLetterHandler] attribute and declared as a partial class.
- For each message type, a private method with the signature ValueTask On{TypeName}({TypeName} letter) is created.
- At compile time, the generator creates a partial class extension with OnLetter containing a switch over all discovered handlers.
- Message types must be sealed and inherit from Letter.

Use of the generator is optional. Actors not marked with the attribute must override OnLetter manually, as before. Full backward compatibility is maintained.

**26. XML-based actor model (extension)**
Declarative construction of an actor model from an XML description is moved to the separate MinimalActorSystem.XmlModel assembly. This extension is not part of the core and is included only when needed.

The assembly contains CompiledModelActor, CompiledModel, ElementConfig, ElementRule, and XmlModelCompiler. The system core does not depend on this extension and contains no references to XML. Imperative model construction through ModelActor remains the primary method and is located in the core.

**27. Naming conventions**
The following conventions are adopted to improve code readability:
- Letter class names end with Letter (e.g., PingLetter, SensorDataLetter).
- Actor class names end with Actor (e.g., PingActor, SensorActor).

These conventions are not enforced by the system but are recommended for all application projects. The ActorLetterHandlerGenerator source generator relies on the On{TypeName} convention for handler methods.