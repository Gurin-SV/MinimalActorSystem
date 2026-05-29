# Manifest of Minimalistic Actor System

**1. Complete minimalism. Nothing superfluous.**
Every element of the system must have an unambiguous reason for existence.
If functionality can be moved outside the core — it is moved.

**2. Manifest as foundation and memory.**
All subsequent points serve two purposes:
- serve as a foundation for writing code (architectural decisions are derived from the manifest points);
- restore context after memory overflow (just read the manifest to continue development).

**3. Runtime environment**
Programming language — C#, target platform — .NET. All implementation decisions must use the capabilities of this ecosystem (including System.Threading.Channels, Task, Span, platform hardware features), but without unnecessary dependencies.

**4. Isolation and interaction**
Actors are parallel by nature and completely independent of each other. The only way for actors to interact is through asynchronous message exchange. No shared mutable state, no direct synchronous calls between actors.

**5. Incoming message queue**
Each actor has an incoming message queue. Messages are processed strictly sequentially — in the order they arrived in the queue. No concurrent processing inside a single actor instance: at any given moment, an actor processes exactly one message.

**6. Nature of a message**
A message is an object passed between actors. It can be:
- a request (one-way request);
- a response to a previous message;
- an accumulator that mutates as it is sequentially passed from actor to actor (movable mutable request).

**7. Lightweight actors**
The maximum number of actors in the system is limited solely by available resources (memory, CPU). There are no architectural restrictions on the number of instances.

**8. Actor activity**
An actor is active only while processing a message from its queue. The rest of the time it consumes no CPU time, holds no threads, and exhibits no background activity. It follows that an actor is essentially state + message queue. Behavior is a function that executes when a message arrives and then goes to sleep.

**9. Actor identification**
Each actor has a unique identifier — a Guid. The identifier is assigned when the actor is created and does not change throughout its lifecycle.

**10. Actor registry**
The actor system contains an internal dictionary Guid → Actor. This is the only place in the system where a direct reference to an actor instance is stored. External code does not have direct access to this dictionary.

**11. Send method**
Sending a message to an actor is done through the actor system's public method:

    Send(letter: Letter)

This is the only way to deliver a message to an actor. The method finds the actor in the registry by identifier and places the message into its queue.

**12. Message typing**
Messages are strongly typed. The message type is always a sealed class; message inheritance is prohibited. The purpose, structure, and semantics of a message are fully determined by its type. All messages inherit from the abstract class Letter, which contains two fields: Sender (Guid of sender) and Receiver (Guid of receiver). The message classes themselves are sealed; semantic inheritance is prohibited.

**13. Actor system is instance-based**
The actor system is an instance object. No static references, global state, or singletons. Multiple independent actor systems may exist within a single application or service.

**14. Actor access to the system**
A reference to the actor system is passed to the actor through the constructor when it is created. This is the only way for an actor to be able to send messages to other actors.

**15. Actor creation**
The initial set of actors that form the actor system's connections to external services (I/O, HTTP, queues, etc.) is created during actor system initialization. One of these actors is the root parent and acts as a factory for the application task, creating the first layer of application actors.

Application actors are created dynamically — the parent spawns a child through the actor system and receives its identifier (Guid). The child, in turn, can create its own descendants, forming an actor tree.

System actors have constant identifiers (Guid) available through the static class SystemUids. This allows application actors to send messages to system actors directly, without needing to search by type or name. System actors are independent of the specific application program and application actors, but for each system actor, the set of messages it can receive is fixed. System actors cannot send messages to application actors except in response.

The application actor parent is an intermediate link between system and application actors. It has a constant identifier (SystemUids.Model). Model construction is imperative — through inheritance from ModelActor. The descendant overrides OnBuildModel() and creates application actors through the Create() method. ModelActor receives InitializeLetter, processes it, and calls OnBuildModel. On error in OnBuildModel, it calls Panic on the actor system.

Only one model actor can exist in an actor system.

**16. Logging**
The logger is a simple object (not an actor) accessible through the actor system's property. All actors call logger methods directly, without sending messages. The choice of a simple object was made for efficiency: logging should not go through the message queue and actor thread.

This is an important clarification: not everything in the system must be an actor. Utility cross-cutting concerns remain objects.

**17. Error handling**
Errors do not propagate beyond the actor. Each actor handles exceptions that occur while processing a message internally. If a message cannot be processed, the actor logs the details and continues working with the next message.

**18. Panic shutdown**
If an error makes further operation of an actor impossible, the actor logs the reason (CriticalError) and calls the actor system's Panic method. The actor system sets the IsPanic flag and cancels the CancellationToken.

Each actor, upon detecting token cancellation:
- exits the message processing loop;
- calls OnShutdown to properly release resources;
- removes itself from the registry.

When the actor registry becomes empty, WaitForShutdownAsync completes. External code checks IsPanic to determine the exit code (0 — normal shutdown, 1 — panic).

**19. Ignoring unknown messages**
If an actor receives a message of an unknown type, it ignores it (with possible logging). An actor can accept any messages but only processes those for which it has a handler defined.

This allows actor versioning: older implementations may not know about new message types and continue to work stably, while newer implementations handle an extended set. The system evolves smoothly, without needing to update all actors simultaneously. An actor is an elastic handler: unknown doesn't break, new is added gradually.

**20. Actor lifecycle**
The actor lifecycle consists of three phases:

- Creation. In the constructor, the actor initializes all its internal structures and message queue. After creation, the actor is registered in the registry and immediately starts the message processing loop. The actor is ready to receive messages immediately after registration.
- Active phase. The actor spends most of its life waiting for messages from its queue and processing them sequentially.
- Termination. Upon detecting CancellationToken cancellation (during Shutdown or Panic), the actor exits the message processing loop, calls OnShutdown, where it carefully finalizes state and releases resources, then calls UnregisterActor, removing itself from the registry.

After termination, the actor cannot become active again.

**21. Shutdown waiting and cooperative cancellation**
The actor system provides a public CancellationToken property. The token transitions to the Cancelled state after Panic (abnormal shutdown) or Shutdown (graceful shutdown) is called. Actors use this token for cooperative interruption of internal asynchronous operations when the system is stopping.

The IsPanic property allows external code to determine the reason for termination: false for Shutdown, true for Panic.

The WaitForShutdownAsync method asynchronously waits for the actor registry to become empty.

**22. TimeService**
The actor system provides a time service — a simple object (not an actor) accessible to actors through a public property of the actor system.

The time service provides:
- current UTC time — the only way for actors to get the time;
- methods for registering and canceling timeouts, used by actors to implement timeout operations.

Purposes of the time service:

Time substitution in tests. In production, the service provides real system time (SystemTimeService). In test mode — virtual time (VirtualTimeService), allowing long intervals to execute instantly. This provides fast and deterministic unit tests without real delays.

Universal timeout error criterion. Actors use the time service to track timeouts (e.g., during reconnection). If an action is not completed within a given virtual time interval, it is considered failed and requires either retry or other reaction.

Operating modes. The Settings.TimeServiceModes setting determines how the system interacts with the time service:
- System — production mode with SystemTimeService, messages are queued asynchronously;
- Async — test mode with VirtualTimeService and asynchronous processing, activity tracked via IncrementActivity/DecrementActivity;
- Sync — test mode with VirtualTimeService and synchronous message processing directly in the sender's thread.

**23. System Settings**
The actor system contains a settings object — a simple object (not an actor) accessible to actors through a public property of the actor system.

Purpose of the settings object:
- store configuration parameters of the actor system and actors;
- provide actors with a single point of access to settings without directly accessing static classes, environment variables, or configuration files.

The settings object:
- is immutable after actor system initialization — changes to settings during operation are not allowed;
- contains the required field TimeServiceModes (time service operating mode: System, Async, or Sync);
- may contain additional parameters: timeouts, limits, mode flags, logging levels, queue buffer sizes, etc.;
- in the test environment can be replaced with a specialized test configuration.

**24. Metrics**
The actor system provides metrics — a simple object (not an actor) accessible to actors through a public property of the actor system.

Purpose of metrics:
- provide system counters: number of messages sent and dropped, actors created and destroyed, current registry size;
- provide actors with a standard Meter (System.Diagnostics.Metrics) for creating application metrics without additional dependencies.

Metrics follow the same principles as the logger and time service:
- replaceable implementation via the IActorSystemMetrics interface;
- default no-op implementation — all calls are ignored, zero overhead;
- production implementation (SystemMetrics) uses standard System.Diagnostics.Metrics and integrates with OpenTelemetry, Prometheus, and other collectors;
- application actors can create their own instruments via System.Metrics.Meter without depending on the specific implementation.

**25. Automatic message dispatch (Source Generator)**
The actor system includes a source generator ActorLetterHandlerGenerator that eliminates the need to manually write the OnLetter method with a switch. The generator is located in a separate assembly MinimalActorSystem.SourceGenerator and adds no runtime dependencies.

How it works:
- The actor is marked with the [ActorLetterHandler] attribute and declared as a partial class.
- For each letter type, a private method with the signature ValueTask On{TypeName}({TypeName} letter) is created.
- The generator at compile time creates a partial addition to the class with OnLetter containing a switch over all found handlers.
- Letter types must be sealed and inherit from Letter.

Using the generator is optional. Actors not marked with the attribute must override OnLetter manually, as before. Backward compatibility is complete.

**26. XML-based actor model (extension)**
Declarative construction of an actor model from an XML description is extracted into a separate assembly MinimalActorSystem.XmlModel. This extension is not part of the core and is only added when needed.

The assembly contains CompiledModelActor, CompiledModel, ElementConfig, ElementRule, and XmlModelCompiler. The core system does not depend on this extension and contains no mentions of XML. Imperative model construction via ModelActor remains the primary method and resides in the core.

**27. Naming conventions**
To improve code readability, the following conventions are adopted:
- Letter class names end with "Letter" (e.g., PingLetter, SensorDataLetter).
- Actor class names end with "Actor" (e.g., PingActor, SensorActor).

These conventions are not dictated by the system but are recommended for all application projects. The ActorLetterHandlerGenerator source generator relies on the On{TypeName} convention for handler methods.
