namespace MinimalActorSystem;

/// <summary>
/// Константные идентификаторы системных акторов с фиксированными значениями.
/// Доступны статически через <c>SystemUids.System</c> и т.д.
/// Используются для адресации системных акторов при отправке писем.
/// Для других "системных" акторов конкретное приложение должно использовать другой
/// класс, например, ApplicationUids, но в нём нужно определять идентификаторы 
/// через Guid.NewGuid()
/// </summary>
public static class SystemUids
{
    /// <summary>
    /// Идентификатор системного актора. Используется как отправитель по умолчанию.
    /// </summary>
    public static readonly Guid System = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Идентификатор сервиса времени. Отправляет <see cref="TimeServiceLetter"/> при срабатывании таймаутов.
    /// </summary>
    public static readonly Guid TimeService = new("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Идентификатор модельного актора (<see cref="ModelActor"/>). Корневой родитель прикладных акторов.
    /// </summary>
    public static readonly Guid Model = Guid.NewGuid();
}
