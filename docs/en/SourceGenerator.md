# SourceGenerator: Automatic Letter Dispatching

## Purpose

The `ActorLetterHandlerGenerator` source generator eliminates the need to manually write the `OnLetter` method with a `switch` in `Actor` subclasses. The generator automatically discovers letter handlers in the class and generates the dispatching logic at compile time.

## Setup

Add a project reference to the generator in the csproj file:

    <ItemGroup>
      <ProjectReference Include="..\MinimalActorSystem.SourceGenerator\MinimalActorSystem.SourceGenerator.csproj"
                        OutputItemType="Analyzer"
                        ReferenceOutputAssembly="false" />
    </ItemGroup>

The `MinimalActorSystem` core must be referenced as usual.

## Usage

### 1. Mark the actor with the attribute

The actor class must be `partial` and marked with the `[ActorLetterHandler]` attribute:

    [ActorLetterHandler]
    public partial class MyActor : Actor
    {
        // ...
    }

### 2. Create letter handlers

For each letter type the actor should handle, create a private method with the following signature:

    private ValueTask On{LetterTypeName}({LetterTypeName} letter)

The method name is strictly tied to the letter type name: for the type `Ping` the method must be named `OnPing`, for `SensorDataLetter` — `OnSensorDataLetter`, etc.

Example:

    [ActorLetterHandler]
    public partial class PingActor : Actor
    {
        public PingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name) { }

        private ValueTask OnPing(Ping ping)
        {
            // handle Ping
            return default;
        }

        private ValueTask OnPong(Pong pong)
        {
            // handle Pong
            return default;
        }
    }

### 3. Generated code

The generator automatically creates a partial class extension with the `OnLetter` override:

    public partial class PingActor
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case Ping msg:
                    return OnPing(msg);
                case Pong msg:
                    return OnPong(msg);
                default:
                    return default;
            }
        }
    }

No other code is required in `OnLetter` — only dispatching. All business logic remains in individual handler methods.

## Constraints

- **The letter type must be `sealed`.** This is a manifesto requirement and is enforced by the generator.
- **The letter type must inherit from `Letter`.** Verified by walking up the base class chain.
- **The method name must be `On{TypeName}`.** IntelliSense in Visual Studio requires this convention to recognize the abstract method implementation. Without it, the IDE shows an error even though compilation succeeds.
- **Methods must be `private`.** The generator ignores methods with other access modifiers.
- **Methods must return `ValueTask`.** The signature is fixed: exactly one parameter — a sealed letter, return type — `ValueTask`.

## How It Works

1. At compile time, the generator scans all partial classes marked with the `[ActorLetterHandler]` attribute.
2. For each class, it collects private methods matching the signature: `ValueTask On{Type}({Type} letter)`, where `{Type}` is a sealed class inheriting from `Letter`.
3. It generates a partial extension with `OnLetter` containing a `switch` over all discovered types.
4. The compiler merges the original and generated partials into a single class.

## Components

- **`MinimalActorSystem`** — the `ActorLetterHandlerAttribute` (empty marker attribute).
- **`MinimalActorSystem.SourceGenerator`** — the `ActorLetterHandlerGenerator`.
- **Consumer project** — partial actors with the attribute and handler methods.