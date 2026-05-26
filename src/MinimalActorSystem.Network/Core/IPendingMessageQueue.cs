namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс очереди отложенных сообщений.
/// </summary>
/// <remarks>
/// Pending message queue interface.
/// </remarks>
public interface IPendingMessageQueue
{
    /// <summary>
    /// Помещает сообщение в очередь.
    /// </summary>
    /// <param name="message">Отложенное сообщение.</param>
    void Enqueue(PendingMessage message);

    /// <summary>
    /// Извлекает все неотправленные отложенные сообщения для указанного узла.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <returns>Список сообщений для узла (после извлечения удаляются из очереди).</returns>
    IReadOnlyList<PendingMessage> DequeueForNode(string nodeName);

    /// <summary>
    /// Удаляет сообщение из очереди по локальному идентификатору.
    /// </summary>
    /// <param name="localMessageId">Локальный идентификатор сообщения.</param>
    /// <returns>true, если сообщение найдено и удалено, иначе false.</returns>
    bool Remove(Guid localMessageId);

    /// <summary>
    /// Проверяет, находится ли сообщение в очереди.
    /// </summary>
    /// <param name="localMessageId">Локальный идентификатор сообщения.</param>
    /// <returns>true, если сообщение в очереди, иначе false.</returns>
    bool Contains(Guid localMessageId);

    /// <summary>
    /// Получает сообщение по локальному идентификатору.
    /// </summary>
    /// <param name="localMessageId">Локальный идентификатор сообщения.</param>
    /// <returns>Сообщение или null, если не найдено.</returns>
    PendingMessage? Get(Guid localMessageId);

    /// <summary>
    /// Количество сообщений в очереди.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Количество отложенных сообщений для указанного узла.
    /// </summary>
    int GetQueuedCountForNode(string nodeName);

    /// <summary>
    /// Очищает очередь.
    /// </summary>
    void Clear();
}
