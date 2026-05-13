# MinimalActorSystem: Tutorial and Quick Start

MinimalActorSystem is a minimalistic actor system for C# designed for applications that need lightweight concurrency without heavy frameworks. Actors are isolated, communicate only through messages, do not share mutable state, and do not block threads while waiting. The system requires no external dependencies except the standard .NET library and Microsoft.Extensions.Logging.

This tutorial will guide you from installation to writing your first actor and testing with virtual time. It assumes you are already familiar with C# and async/await.

## Quick Start

Create a .NET console application and add a reference to the MinimalActorSystem assembly. If you cloned the repository, just add the project to your solution.

A minimal program that runs an actor system with one actor looks like this:

    using MinimalActorSystem;
    using Microsoft.Extensions.Logging;

    // 1. Create settings
    var settings = new Settings
    {
        TimeServiceModes = TimeServiceModes.System
    };

    // 2. Create actor system
    var system = new ActorSystem(settings);

    // 3. Add logger
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    system.Logger = loggerFactory.CreateLogger("ActorSystem");

    // 4. Add time service
    system.TimeService = new SystemTimeService(system);

    // 5. Create model actor
    var model = new MyModelActor(system);
    system.RegisterActor(model);

    // 6. Start model building
    model.SendInitialize();

    // 7. Wait for completion (Ctrl+C or other signal)
    Console.WriteLine("Press Enter to exit...");
    Console.ReadLine();

    // 8. Normal shutdown
    system.Shutdown();
    await system.WaitForShutdownAsync();

The `SendInitialize` method is an extension method that sends `InitializeLetter` to the model actor. You can write it yourself:

    public static void SendInitialize(this Actor actor)
    {
        var letter = new InitializeLetter(actor.Uid, actor.Uid);
        actor.System.Send(letter);
    }

After calling `SendInitialize`, the model actor will build the application actors, and the system is ready to work.

## Basic Concepts

**Actor** — an object that owns its state and does not share it with anyone. An actor has a unique identifier (`Guid Uid`), a name for diagnostics, and an incoming message queue. An actor processes messages strictly one at a time. When there are no messages, the actor consumes no CPU time.

**Letter** — a message exchanged between actors. All letters inherit from the abstract class `Letter` and must be `sealed`. A letter contains `Sender` and `Receiver` — the identifiers of the sender and recipient.

**Actor System** — an instance of the `ActorSystem` class that manages the actor registry, delivers letters, and provides the logger, time service, and settings. Multiple independent actor systems can exist within a single application.

**Model Actor** — the root parent of all application actors. It receives `InitializeLetter` and in response creates application actors. Only one model actor can exist in a system; its identifier is `SystemUids.Model`.

## Writing Your First Actor: Ping-Pong

The classic example — two actors that exchange messages a specified number of times.

First, define the letters. `Ping` goes from one actor to another, `Pong` goes back. Both contain a counter of remaining exchanges:

    public sealed class Ping : Letter
    {
        public int Remaining { get; }
        public Ping(Guid sender, Guid receiver, int remaining)
            : base(sender, receiver)
        {
            Remaining = remaining;
        }
    }

    public sealed class Pong : Letter
    {
        public int Remaining { get; }
        public Pong(Guid sender, Guid receiver, int remaining)
            : base(sender, receiver)
        {
            Remaining = remaining;
        }
    }

Now an actor that can receive `Ping` and reply with `Pong`:

    public class PingActor : Actor
    {
        public PingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name) { }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case Ping ping:
                    System.Logger.LogInformation("{Name}: received Ping, {Remaining} left",
                        Name, ping.Remaining);

                    if (ping.Remaining > 0)
                    {
                        var pong = new Pong(Uid, ping.Sender, ping.Remaining - 1);
                        System.Send(pong);
                    }
                    else
                    {
                        System.Logger.LogInformation("{Name}: game over", Name);
                    }
                    break;

                case Pong pong:
                    System.Logger.LogInformation("{Name}: received Pong, {Remaining} left",
                        Name, pong.Remaining);

                    if (pong.Remaining > 0)
                    {
                        var ping = new Ping(Uid, pong.Sender, pong.Remaining - 1);
                        System.Send(ping);
                    }
                    else
                    {
                        System.Logger.LogInformation("{Name}: game over", Name);
                    }
                    break;
            }
            return default;
        }
    }

The model actor creates two players and sends the first `Ping`:

    public class PingPongModel : ModelActor
    {
        public PingPongModel(IActorSystem system) : base(system) { }

        protected override void OnBuildModel()
        {
            var alice = new PingActor(System, Guid.NewGuid(), "Alice");
            var bob = new PingActor(System, Guid.NewGuid(), "Bob");

            Create(alice);
            Create(bob);

            // Send first Ping: Alice -> Bob, 5 exchanges
            var firstPing = new Ping(alice.Uid, bob.Uid, 5);
            System.Send(firstPing);
        }
    }

Note: the first `Ping` is sent after both actors are created via `Create`. The `Create` method immediately registers the actor in the system and starts its processing loop, so by the time of sending, both are already active and ready to receive letters.

## Timeouts

Actors can register timeouts through the time service. A timeout is a callback that fires at a specified time and is delivered to the actor as a `TimeServiceLetter`.

To work with timeouts, an actor creates a `TimeoutCallback` in its constructor and registers it via `System.TimeService.Register`. When the timeout fires, the actor receives a `TimeServiceLetter` and calls `callback.Action()`.

Example of an actor with a reconnection timeout:

    public class ReconnectingActor : Actor
    {
        private readonly TimeoutCallback _reconnectTimeout;
        private bool _connected;

        public ReconnectingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            _reconnectTimeout = new TimeoutCallback(Uid, callbackId: 1, OnReconnectTimeout);
        }

        private void OnReconnectTimeout()
        {
            System.Logger.LogWarning("{Name}: connection timeout, trying again", Name);
            TryConnect();
        }

        private void TryConnect()
        {
            // Attempt to connect...
            _connected = true;

            // If not connected — register timeout for another attempt
            if (!_connected)
            {
                System.TimeService.Register(TimeSpan.FromSeconds(5), _reconnectTimeout);
            }
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case TimeServiceLetter timeout:
                    timeout.Callback.Action();
                    break;
            }
            return default;
        }
    }

Important: the timeout callback should not interact with other actors. Its only task is to perform a small action inside the owner actor.

## Metrics

The actor system provides built-in metrics for monitoring. Enabling and configuration are similar to the logger and time service — via a replaceable object.

### Enabling Metrics

    using MinimalActorSystem;

    var system = new ActorSystem(settings);
    system.Logger = loggerFactory.CreateLogger("ActorSystem");
    system.TimeService = new SystemTimeService(system);
    system.Metrics = new SystemMetrics("MyApp");  // enables metrics

By default, `NullMetrics` is used — all calls are ignored, zero overhead. When `SystemMetrics` is set, the system starts collecting:

- `messages.sent` — total messages sent
- `messages.dropped` — dropped due to queue overflow
- `actors.created` — total actors created
- `actors.destroyed` — total actors destroyed
- `actors.active` — current number of actors in the registry

### Application Metrics

Actors can create their own metrics using the standard `Meter` from `System.Diagnostics.Metrics`:

    public class PaymentActor : Actor
    {
        private readonly Counter<long>? _paymentsProcessed;

        public PaymentActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            var meter = system.Metrics.Meter;
            if (meter != null)
            {
                _paymentsProcessed = meter.CreateCounter<long>(
                    "payments.processed",
                    description: "Total payments processed");
            }
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PaymentReceived payment)
            {
                // Process payment...
                _paymentsProcessed?.Add(1);
            }
            return default;
        }
    }

The `System.Metrics.Meter` property returns `null` if metrics are not configured — the actor simply does not create instruments. When metrics are enabled, all counters go to the same `Meter` as the system metrics and are exported uniformly.

### Integration with OpenTelemetry

Metrics use standard `System.Diagnostics.Metrics`, so they integrate with any collector without additional dependencies in the core:

    using OpenTelemetry;
    using OpenTelemetry.Metrics;

    using var meterProvider = Sdk.CreateMeterProviderBuilder()
        .AddMeter("MyApp")
        .AddOtlpExporter()
        .Build();

    // ... system work ...

    meterProvider.ForceFlush();

Metrics will be delivered to OpenTelemetry Collector, Prometheus, Grafana, or any other compatible collector.

### Test Implementation

To check metrics in tests, use `LoggerMetrics` from the `MinimalActorSystem.Testing` namespace:

    using MinimalActorSystem.Testing;

    var logger = new CallbackLogger(msg => TestContext.WriteLine(msg));
    system.Metrics = new LoggerMetrics(logger);

    // After the test, all metric calls will be visible in the logs:
    // Metric: message.sent
    // Metric: actor.created
    // Metric: actors.active = 42

## Testing with Virtual Time

The main advantage of MinimalActorSystem is the ability to test scenarios with long time intervals instantly, without real delays.

Tests use `VirtualTimeService` and the `TimeServiceModes.Sync` (synchronous message processing) or `TimeServiceModes.Async` (asynchronous with waiting) setting.

### Synchronous Test Mode

Synchronous mode is the simplest. Letters are processed immediately in the calling thread, and timeouts fire when virtual time advances.

    using MinimalActorSystem;
    using MinimalActorSystem.Testing;

    [Test]
    public void PingPong_CompletesInFiveExchanges()
    {
        // 1. Setup
        var settings = new Settings { TimeServiceModes = TimeServiceModes.Sync };
        var system = new ActorSystem(settings);

        // Logger for debugging
        system.CreateTestLogger(TestContext.WriteLine);

        // Virtual time
        var timeService = new VirtualTimeService(system);
        timeService.SetTime("01.01.2024 12:00:00".AsUtc());
        system.TimeService = timeService;

        // 2. Model
        var model = new PingPongModel(system);
        system.RegisterActor(model);
        model.SendInitialize();

        // 3. Start virtual clock (if time advancement is needed)
        timeService.StartVirtualClock(
            startTime: "01.01.2024 12:00:00".AsUtc(),
            endTime: "01.01.2024 12:01:00".AsUtc(),
            step: TimeSpan.FromSeconds(1)
        );

        // 4. Assertions...
    }

`StartVirtualClock` goes from `startTime` to `endTime` with step `step`. At each step, pending timeout registrations/cancellations are applied, expired timeouts fire, and virtual time subscribers are called (see below). In synchronous mode, all letters are processed immediately.

### Asynchronous Test Mode

Asynchronous mode uses `StartVirtualClockAsync` and `TimeServiceModes.Async`. After each virtual time step, the system waits for all actors to process their queues (`WaitAllIdleAsync`). This allows testing scenarios where the order of message processing matters.

    [Test]
    public async Task PingPongAsync_Completes()
    {
        var settings = new Settings { TimeServiceModes = TimeServiceModes.Async };
        var system = new ActorSystem(settings);
        system.CreateTestLogger(TestContext.WriteLine);

        var timeService = new VirtualTimeService(system);
        timeService.SetTime("01.01.2024 12:00:00".AsUtc());
        system.TimeService = timeService;

        var model = new PingPongModel(system);
        system.RegisterActor(model);
        model.SendInitialize();

        await timeService.StartVirtualClockAsync(
            startTime: "01.01.2024 12:00:00".AsUtc(),
            endTime: "01.01.2024 12:01:00".AsUtc(),
            step: TimeSpan.FromSeconds(1)
        );
    }

### External Environment Simulators

Tests often need to simulate the external environment: data sources, connection failures, configuration changes. For this, the `IVirtualTimeSubscriber` interface is used.

A subscriber gains control at each virtual time step and can send a letter to an actor, simulating an external event:

    public class TelemetrySimulator : IVirtualTimeSubscriber
    {
        private readonly IActorSystem _system;
        private readonly Guid _targetActor;

        public string Name => "TelemetrySimulator";

        public TelemetrySimulator(IActorSystem system, Guid targetActor)
        {
            _system = system;
            _targetActor = targetActor;
        }

        public ValueTask OnTimeStep(DateTime currentTime)
        {
            // Simulate telemetry arrival every 10 seconds
            if (currentTime.Second % 10 == 0)
            {
                var data = new TelemetryData(/* ... */);
                _system.Send(new TelemetryLetter(
                    sender: SystemUids.System,
                    receiver: _targetActor,
                    data: data
                ));
            }
            return default;
        }
    }

The subscriber is registered with `VirtualTimeService`:

    var simulator = new TelemetrySimulator(system, someActor.Uid);
    timeService.Subscribe(simulator);

Now, at each call to `StartVirtualClock` or `StartVirtualClockAsync`, the simulator will gain control at each step and send letters according to the model time.

## Declarative Model Construction from XML

If the application system contains many actors with complex relationships, instead of imperative creation in `ModelActor.OnBuildModel`, you can use `CompiledModelActor` and an XML description.

XML model description:

    <Model>
      <Sensor Uid="a1b2c3d4-...">
        <Name>TemperatureSensor</Name>
        <Interval>0x3E8</Interval>
      </Sensor>
      <Controller Uid="e5f6a7b8-...">
        <SetPoint>22.5</SetPoint>
        <Deadband>0.5</Deadband>
      </Controller>
    </Model>

Compilation and usage:

    public class MyCompiledModelActor : CompiledModelActor
    {
        public MyCompiledModelActor(IActorSystem system) : base(system) { }

        protected override CompiledModel CompileModel()
        {
            var compiler = new XmlModelCompiler("Uid")
                .AddRule("Sensor", new ElementRule()
                    .WithRequired("Uid")
                    .WithProperties("Name", "Interval"))
                .AddRule("Controller", new ElementRule()
                    .WithRequired("Uid")
                    .WithProperties("SetPoint", "Deadband"));

            var xml = File.ReadAllText("model.xml");
            return compiler.Compile(xml);
        }

        protected override object CreateObject(ElementConfig element)
        {
            return element.ElementType switch
            {
                "Sensor" => CreateSensor(element),
                "Controller" => CreateController(element),
                _ => throw new InvalidOperationException($"Unknown element type: {element.ElementType}")
            };
        }

        private SensorActor CreateSensor(ElementConfig element)
        {
            element.TryGetString("Name", out var name);
            element.TryGetInt32("Interval", out var interval);
            return new SensorActor(System, element.Uid, name, interval);
        }

        private ControllerActor CreateController(ElementConfig element)
        {
            element.TryGetDouble("SetPoint", out var setPoint);
            element.TryGetDouble("Deadband", out var deadband);
            return new ControllerActor(System, element.Uid, setPoint, deadband);
        }

        protected override void OnAfterCreate(CompiledModel model)
        {
            // Establish connections between actors
            var sensor = GetObject<SensorActor>(/* sensor uid */);
            var controller = GetObject<ControllerActor>(/* controller uid */);
            sensor.SetController(controller);
        }
    }

The `OnAfterCreate` method is called after all objects are created, but before they are registered in the system. You cannot send letters here — recipients are not yet registered. But you can set direct references between actors if needed for configuration.

## Error Handling and Termination

An actor should catch exceptions inside `OnLetter`. An unhandled exception is logged, and the actor continues working with the next message.

If an error makes further operation of the actor impossible, it calls `System.Panic()`. This sets the `IsPanic` flag and cancels the cancellation token, causing all actors to terminate. External code waiting via `WaitForShutdownAsync` can check `system.IsPanic` and exit with a non-zero return code.

Normal shutdown is initiated by calling `System.Shutdown()`. Actors call `OnShutdown`, release resources, and remove themselves from the registry.

## Summary

MinimalActorSystem gives you:

- Isolated actors with sequential message processing
- Asynchronous lock-free message delivery
- Time service with timeout support
- Virtual time for fast deterministic tests
- External environment simulators via `IVirtualTimeSubscriber`
- Declarative model construction from XML for complex configurations
- Built-in file and console logging

The system remains minimalistic: no supervisors, no message inheritance hierarchies, no hot code swapping, no distributed transactions. Just actors, letters, queues, and time.