namespace MinimalActorSystem;

public sealed class Settings
{
    public int DefaultQueueCapacity { get; init; } = 200;
    public bool IsProduction { get; init; } = true;
    public bool SynchronousProcessing { get; init; } = false;
}
