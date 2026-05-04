namespace MinimalActorSystem;

public interface ITimeService
{
    DateTime UtcNow { get; }
    void Register(DateTime deadline, TimeoutCallback callback);
    void Register(TimeSpan timeout, TimeoutCallback callback);
    void Unregister(TimeoutCallback callback);
}
