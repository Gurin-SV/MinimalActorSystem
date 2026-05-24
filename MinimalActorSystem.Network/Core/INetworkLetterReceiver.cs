namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс-маркер для писем, поддерживающих сетевую отправку (имеющих свойство ReceiverNodeName).
/// Это конвенция, не требующая обязательной реализации - свойство может быть добавлено без интерфейса.
/// </summary>
/// <remarks>
/// Marker interface for letters supporting network sending (having ReceiverNodeName property).
/// This is a convention - the property can be added without implementing this interface.
/// </remarks>
public interface INetworkLetterReceiver
{
    /// <summary>
    /// Имя узла-получателя.
    /// </summary>
    string ReceiverNodeName { get; }
}
