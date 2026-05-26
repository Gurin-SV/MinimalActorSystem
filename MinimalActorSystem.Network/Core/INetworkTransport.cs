namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс транспортного уровня для отправки сетевых сообщений.
/// Предоставляет метод для отправки сообщений и возможность установки обработчика входящих сообщений.
/// </summary>
/// <remarks>
/// Transport layer interface for sending network messages.
/// Provides a method for sending messages and the ability to set an incoming message handler.
/// </remarks>
public interface INetworkTransport : IDisposable
{
    /// <summary>
    /// Устанавливает обработчик входящих сообщений.
    /// Вызывается транспортной реализацией при получении сообщения от другого узла.
    /// </summary>
    /// <param name="onMessageReceived">Делегат, вызываемый при получении сообщения.
    /// Параметры: имя узла-отправителя, сериализованное сообщение.</param>
    /// <remarks>
    /// Sets the incoming message handler.
    /// Called by the transport implementation when a message is received from another node.
    /// </remarks>
    void SetMessageHandler(Func<string, string, Task> onMessageReceived);

    /// <summary>
    /// Отправляет сериализованное сообщение на указанный узел.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла-получателя.</param>
    /// <param name="serializedMessage">Сериализованное сетевое сообщение.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию отправки.</returns>
    /// <remarks>
    /// Sends a serialized message to the specified node.
    /// </remarks>
    Task SendAsync(string nodeName, string serializedMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Регистрирует текущий узел в транспорте.
    /// Вызывается при старте узла.
    /// </summary>
    /// <param name="nodeName">Символическое имя текущего узла.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию регистрации.</returns>
    /// <remarks>
    /// Registers the current node in the transport.
    /// Called when the node starts.
    /// </remarks>
    Task RegisterNodeAsync(string nodeName, CancellationToken cancellationToken = default);
}
