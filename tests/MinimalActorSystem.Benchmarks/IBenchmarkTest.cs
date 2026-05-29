namespace MinimalActorSystem.Benchmarks;

public interface IBenchmarkTest
{
    string Name { get; }
    string Description { get; }
    Task RunAsync();
}
