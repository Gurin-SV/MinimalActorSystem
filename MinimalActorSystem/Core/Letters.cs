namespace MinimalActorSystem;

public abstract class Letter(Guid sender, Guid receiver)
{
    public Guid Sender { get; set; } = sender;
    public Guid Receiver { get; set; } = receiver;
}

public sealed class ShutdownLetter(Guid sender, Guid receiver)
    : Letter(sender, receiver);

public sealed class PanicLetter(Guid sender, Guid receiver)
    : Letter(sender, receiver);

public sealed class TimeServiceLetter(Guid sender, Guid receiver, TimeoutCallback callback)
    : Letter(sender, receiver)
{
    public TimeoutCallback Callback { get; } = callback;
}
