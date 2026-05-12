using MinimalActorSystem;

namespace Demo.Common.Philosophers;

public sealed class TakeForkRequestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
public sealed class ForkTakenResponseLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
public sealed class ForkBusyResponseLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
public sealed class PutForkRequestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
public sealed class StartLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
public sealed class StopLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
