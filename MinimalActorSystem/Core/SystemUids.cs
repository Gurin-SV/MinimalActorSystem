namespace MinimalActorSystem;

/// <summary>
/// Константные идентификаторы системных акторов с фиксированными значениями.
/// Доступны статически через <c>SystemUids.System</c> и т.д.
/// Используются для адресации системных акторов при отправке писем.
/// Для других "системных" акторов конкретное приложение должно использовать другой
/// класс, например, ApplicationUids, но в нём нужно определять идентификаторы 
/// через Guid.NewGuid()
/// </summary>
/// <remarks>
/// Constant UIDs for system actors. Used for addressing when sending letters.
/// For application-specific "system" actors, use a separate class like ApplicationUids
/// and generate UIDs via Guid.NewGuid().
/// </remarks>
public static class SystemUids
{
    /// <summary>
    /// Идентификатор системного актора. Используется как отправитель по умолчанию.
    /// </summary>
    /// <remarks>
    /// System actor UID. Used as default sender.
    /// </remarks>
    public static readonly Guid System = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Идентификатор сервиса времени. Отправляет <see cref="TimeServiceLetter"/> при срабатывании таймаутов.
    /// </summary>
    /// <remarks>
    /// Time service actor UID. Sends <see cref="TimeServiceLetter"/> when timeouts fire.
    /// </remarks>
    public static readonly Guid TimeService = new("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Идентификатор модельного актора (<see cref="ModelActor"/>). Корневой родитель прикладных акторов.
    /// </summary>
    /// <remarks>
    /// Model actor UID (<see cref="ModelActor"/>). Root parent for application actors.
    /// Note: Unlike System and TimeService, this is a generated Guid (not fixed).
    /// </remarks>
    public static readonly Guid Model = Guid.NewGuid();
}
