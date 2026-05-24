namespace MinimalActorSystem.Network;

/// <summary>
/// Сетевое письмо для передачи обычных писем между узлами.
/// </summary>
/// <remarks>
/// Network letter for transferring regular letters between nodes.
/// </remarks>
public sealed class DefaultNetworkLetter : NetworkLetter
{
    /// <summary>
    /// Конструктор по умолчанию для десериализации.
    /// </summary>
    public DefaultNetworkLetter() { }

    /// <summary>
    /// Создаёт сетевое письмо с указанными параметрами.
    /// </summary>
    /// <param name="receiverNodeName">Имя узла-получателя.</param>
    /// <param name="serializedLetter">Сериализованное письмо.</param>
    /// <param name="letterTypeName">Тип письма.</param>
    public DefaultNetworkLetter(string receiverNodeName, string serializedLetter, string letterTypeName)
    {
        ReceiverNodeName = receiverNodeName;
        SerializedLetter = serializedLetter;
        LetterTypeName = letterTypeName;
    }
}
