namespace MinimalActorSystem;

/// <summary>
/// Коллбек таймаута. Создаётся актором один раз в конструкторе и может многократно
/// регистрироваться в <see cref="ITimeService"/> с разными deadline.
/// Уникальность определяется парой (<see cref="ActorUid"/>, <see cref="CallbackId"/>).
/// При срабатывании таймаута доставляется актору через <see cref="TimeServiceLetter"/>,
/// где актор вызывает <see cref="Action"/>.
/// </summary>
/// <remarks>
/// Timeout callback. Created once per actor and can be reused with different deadlines.
/// Uniqueness is determined by the pair (ActorUid, CallbackId).
/// On timeout, delivered to the actor via TimeServiceLetter.
/// </remarks>
/// <param name="actorUid">Идентификатор актора, зарегистрировавшего таймаут.</param>
/// <param name="callbackId">Уникальный в рамках актора идентификатор коллбека. Назначается актором.</param>
/// <param name="action">Действие, выполняемое при срабатывании таймаута.</param>
public readonly struct TimeoutCallback(Guid actorUid, int callbackId, Action action) : IEquatable<TimeoutCallback>
{
    /// <summary>
    /// Идентификатор актора, которому принадлежит таймаут.
    /// </summary>
    public Guid ActorUid { get; } = actorUid;

    /// <summary>
    /// Уникальный в рамках актора идентификатор коллбека.
    /// </summary>
    public int CallbackId { get; } = callbackId;

    /// <summary>
    /// Действие, которое будет выполнено при срабатывании таймаута.
    /// </summary>
    public Action Action { get; } = action;

    /// <inheritdoc/>
    public bool Equals(TimeoutCallback other)
        => ActorUid == other.ActorUid && CallbackId == other.CallbackId;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is TimeoutCallback other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(ActorUid, CallbackId);

    /// <summary>Сравнивает два коллбека на равенство.</summary>
    public static bool operator ==(TimeoutCallback left, TimeoutCallback right) => left.Equals(right);

    /// <summary>Сравнивает два коллбека на неравенство.</summary>
    public static bool operator !=(TimeoutCallback left, TimeoutCallback right) => !left.Equals(right);
}
