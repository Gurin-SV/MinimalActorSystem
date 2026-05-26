namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс сериализатора сетевых сообщений.
/// </summary>
/// <remarks>
/// Network message serializer interface.
/// </remarks>
public interface IMessageSerializer
{
    /// <summary>
    /// Сериализует сетевое сообщение в строку.
    /// </summary>
    /// <param name="letter">Сетевое сообщение.</param>
    /// <returns>Сериализованная строка.</returns>
    string Serialize(NetworkLetter letter);

    /// <summary>
    /// Десериализует строку в сетевое сообщение.
    /// </summary>
    /// <param name="data">Сериализованная строка.</param>
    /// <returns>Десериализованное сетевое сообщение или null при ошибке.</returns>
    NetworkLetter? Deserialize(string data);
}
