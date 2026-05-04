namespace MinimalActorSystem;

public readonly struct TimeoutCallback(Guid actorUid, int callbackId, Action action) : IEquatable<TimeoutCallback>
{
    public Guid ActorUid { get; } = actorUid;
    public int CallbackId { get; } = callbackId;
    public Action Action { get; } = action;

    public bool Equals(TimeoutCallback other)
        => ActorUid == other.ActorUid && CallbackId == other.CallbackId;

    public override bool Equals(object? obj)
        => obj is TimeoutCallback other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(ActorUid, CallbackId);

    public static bool operator ==(TimeoutCallback left, TimeoutCallback right) => left.Equals(right);
    public static bool operator !=(TimeoutCallback left, TimeoutCallback right) => !left.Equals(right);
}
