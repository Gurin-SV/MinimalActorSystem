namespace MinimalActorSystem;

/// <summary>
/// Сервис времени. Предоставляет акторам текущее время и механизм регистрации таймаутов.
/// В продакшене используется реализация с реальным временем (<see cref="SystemTimeService"/>),
/// в тестах — реализация с виртуальным временем для быстрых и детерминированных сценариев.
/// </summary>
public interface ITimeService
{
    /// <summary>
    /// Текущее время в UTC. Единственный способ получения времени акторами.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Регистрирует коллбек, который должен быть вызван в указанный момент времени.
    /// Если коллбек с тем же ключом (<see cref="TimeoutCallback.ActorUid"/>, <see cref="TimeoutCallback.CallbackId"/>)
    /// уже зарегистрирован, старая регистрация заменяется новой.
    /// </summary>
    /// <param name="deadline">Момент времени, в который должен сработать коллбек.</param>
    /// <param name="callback">Коллбек, который будет доставлен актору через <see cref="TimeServiceLetter"/>.</param>
    void Register(DateTime deadline, TimeoutCallback callback);

    /// <summary>
    /// Регистрирует коллбек, который должен быть вызван через указанный промежуток времени от текущего момента.
    /// Если коллбек с тем же ключом уже зарегистрирован, старая регистрация заменяется новой.
    /// </summary>
    /// <param name="timeout">Промежуток времени от текущего момента до срабатывания.</param>
    /// <param name="callback">Коллбек, который будет доставлен актору через <see cref="TimeServiceLetter"/>.</param>
    void Register(TimeSpan timeout, TimeoutCallback callback);

    /// <summary>
    /// Удаляет регистрацию коллбека. Если коллбек не найден — ничего не делает.
    /// После вызова таймаут не сработает, даже если deadline ещё не наступил.
    /// </summary>
    /// <param name="callback">Коллбек для удаления. Ключом является пара (ActorUid, CallbackId).</param>
    void Unregister(TimeoutCallback callback);
}
