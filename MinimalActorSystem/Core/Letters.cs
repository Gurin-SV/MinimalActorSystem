namespace MinimalActorSystem;

/// <summary>
/// Базовый абстрактный класс письма. Все сообщения в системе наследуются от него.
/// Содержит идентификаторы отправителя и получателя. Поля мутабельны для поддержки
/// паттерна аккумулятора: то же письмо может быть отправлено обратно со сменой ролей.
/// Все наследники должны быть <c>sealed</c>.
/// </summary>
/// <remarks>
/// Base letter class. Sender and Receiver are mutable to support the accumulator pattern
/// (same letter can be sent back with swapped roles). All derived classes must be sealed.
/// </remarks>
/// <param name="sender">Идентификатор актора-отправителя.</param>
/// <param name="receiver">Идентификатор актора-получателя.</param>
public abstract class Letter(Guid sender, Guid receiver)
{
    /// <summary>
    /// Идентификатор актора-отправителя. Может изменяться при переиспользовании письма.
    /// </summary>
    public Guid Sender { get; set; } = sender;

    /// <summary>
    /// Идентификатор актора-получателя. Может изменяться при переиспользовании письма.
    /// </summary>
    public Guid Receiver { get; set; } = receiver;
}

/// <summary>
/// Письмо от сервиса времени. Доставляется актору при срабатывании зарегистрированного таймаута.
/// Отправитель — <see cref="SystemUids.TimeService"/>.
/// </summary>
/// <remarks>
/// Letter from the time service. Delivered when a registered timeout fires.
/// Sender is <see cref="SystemUids.TimeService"/>.
/// </remarks>
/// <param name="sender">Идентификатор отправителя.</param>
/// <param name="receiver">Идентификатор получателя.</param>
/// <param name="callback">Коллбек, зарегистрированный актором. Содержит действие для выполнения.</param>
public sealed class TimeServiceLetter(Guid sender, Guid receiver, TimeoutCallback callback)
    : Letter(sender, receiver)
{
    /// <summary>
    /// Коллбек таймаута, зарегистрированный актором.
    /// </summary>
    public TimeoutCallback Callback { get; } = callback;
}

/// <summary>
/// Письмо-запрос на завершение конкретного актора. Получатель должен выйти из цикла обработки
/// и вызвать <see cref="Actor.OnShutdown"/>.
/// </summary>
/// <remarks>
/// Shutdown request for a specific actor. The receiver should exit its message loop
/// and call <see cref="Actor.OnShutdown"/>.
/// </remarks>
/// <param name="sender">Идентификатор отправителя.</param>
/// <param name="receiver">Идентификатор получателя.</param>
public sealed class ShutdownLetter(Guid sender, Guid receiver)
    : Letter(sender, receiver)
{
}

/// <summary>
/// Письмо, сигнализирующее <see cref="ModelActor"/> о необходимости начать построение модели.
/// Отправитель — обычно сам <see cref="ModelActor"/> или <see cref="SystemUids.System"/>.
/// </summary>
/// <remarks>
/// Signals <see cref="ModelActor"/> to start model building.
/// Sender is typically the actor itself or <see cref="SystemUids.System"/>.
/// </remarks>
/// <param name="sender">Идентификатор отправителя.</param>
/// <param name="receiver">Идентификатор получателя.</param>
public sealed class InitializeLetter(Guid sender, Guid receiver)
    : Letter(sender, receiver)
{
}
