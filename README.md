# MinimalActorSystem

[![.NET](https://img.shields.io/badge/.NET-8.0%2B-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**Minimalistic actor system for .NET with no external dependencies except Microsoft.Extensions.Logging.**

**2.3M messages/sec · 0.43 μs per message · ~6 KB per actor**

## Project Structure

    MinimalActorSystem/
    ├── src/               # Core and extension libraries
    │   ├── MinimalActorSystem/                 # Core actor system
    │   ├── MinimalActorSystem.SourceGenerator/ # OnLetter source generator
    │   └── MinimalActorSystem.XmlModel/        # XML configuration support
    ├── tests/             # Unit tests
    └── docs/              # Documentation (ru/en)
        ├── ru/
        └── en/

## Philosophy

- Actors communicate **only** via async messages — no shared mutable state
- Each actor has a **single message queue** — one message processed at a time
- Actors consume **no CPU when idle**
- No global state, no singletons — multiple independent systems per application

## Features

| Feature                      | Description                                                       |
|------------------------------|------------------------------------------------------------------|
| **Virtual time**             | Test long timeouts instantly with deterministic time             |
| **Sync test mode**           | Process messages synchronously in a single thread for simple test|
| **Mutable accumulator**      | Reuse the same letter as it travels through actor chain          |
| **XML model**                | Build actor graph declaratively from XML configuration           |
| **High performance**         | 2.3 million messages per second                                  |
| **No external dependencies** | Only `Microsoft.Extensions.Logging`                              |

## Quick Start

    // 1. Create system
    var settings = new Settings { TimeServiceModes = TimeServiceModes.System };
    var system = new ActorSystem(settings);
    
    // 2. Add logger and time service
    system.Logger = loggerFactory.CreateLogger("System");
    system.TimeService = new SystemTimeService(system);
    
    // 3. Create model actor
    var model = new MyModelActor(system);
    system.RegisterActor(model);
    
    // 4. Build the actor graph
    system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));
    
    // 5. Shutdown when done
    system.Shutdown();
    await system.WaitForShutdownAsync();

## Actor Example: Ping-Pong

    public sealed class PingLetter : Letter
    {
        public int Remaining { get; }
        public PingLetter(Guid sender, Guid receiver, int remaining) : base(sender, receiver) { }
    }
    
    public class PingActor : Actor
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingLetter ping && ping.Remaining > 0)
            {
                var pong = new PongLetter(Uid, ping.Sender, ping.Remaining - 1);
                System.Send(pong);
            }
            return default;
        }
    }

## Testing with Virtual Time

    var settings = new Settings { TimeServiceModes = TimeServiceModes.Sync };
    var system = new ActorSystem(settings);
    
    var timeService = new VirtualTimeService(system);
    timeService.SetTime(DateTime.UtcNow);
    system.TimeService = timeService;
    
    // Advance time instantly
    timeService.StartVirtualClock(startTime, endTime, TimeSpan.FromSeconds(1));

## Performance

| Metric               | Value     |
|----------------------|-----------|
| Messages per second  | 2,300,000 |
| Time per message     | 0.43 μs   |
| Memory (6000 actors) | 36.5 MB   |
| Lost messages        | 0         |

## Documentation

| Document | RU | EN |
|----------|----|----|
| Manifest (philosophy & architecture) | [RU](docs/ru/Manifest.md) | [EN](docs/en/Manifest.md) |

## License

MIT
