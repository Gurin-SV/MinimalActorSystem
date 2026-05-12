namespace Demo.Common.Philosophers;

public sealed class PhilosophersSnapshot
{
    public State[] Philosophers { get; } = new State[5];
    public ForkState[] Forks { get; } = new ForkState[5];
}
