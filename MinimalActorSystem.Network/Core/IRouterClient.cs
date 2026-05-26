namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс клиента для взаимодействия с роутером.
/// Обеспечивает регистрацию узла, разрешение имён и получение уведомлений о появлении узлов.
/// </summary>
/// <remarks>
/// Router client interface for interacting with the router.
/// Provides node registration, name resolution, and node discovery notifications.
/// </remarks>
public interface IRouterClient : IDisposable
{
    /// <summary>
    /// Регистрирует текущий узел в роутере.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла.</param>
    /// <param name="nodeAddress">Сетевой адрес узла (URL, IP:port и т.д.).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную операцию регистрации.</returns>
    /// <remarks>
    /// Registers the current node with the router.
    /// </remarks>
    Task RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Разрешает символическое имя узла в сетевой адрес.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сетевой адрес узла или null, если узел не найден.</returns>
    /// <remarks>
    /// Resolves a symbolic node name to a network address.
    /// </remarks>
    Task<string?> ResolveNodeAddressAsync(string nodeName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Запрашивает у роутера список всех известных узлов.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Словарь соответствия имени узла его сетевому адресу.</returns>
    /// <remarks>
    /// Requests the list of all known nodes from the router.
    /// </remarks>
    Task<IReadOnlyDictionary<string, string>> GetAllNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Устанавливает обработчик уведомлений о появлении новых узлов.
    /// Вызывается роутером, когда обнаруживается новый узел.
    /// </summary>
    /// <param name="onNodeDiscovered">Делегат, вызываемый при обнаружении нового узла.
    /// Параметры: имя узла, сетевой адрес.</param>
    /// <remarks>
    /// Sets the notification handler for new node discovery.
    /// Called by the router when a new node is detected.
    /// </remarks>
    void SetNodeDiscoveredHandler(Func<string, string, Task> onNodeDiscovered);

    /// <summary>
    /// Запускает фоновый процесс опроса роутера для получения обновлений.
    /// </summary>
    /// <param name="pollingInterval">Интервал опроса роутера.</param>
    /// <param name="cancellationToken">Токен отмены для остановки опроса.</param>
    /// <returns>Задача, представляющая асинхронную операцию опроса.</returns>
    /// <remarks>
    /// Starts a background polling process to get updates from the router.
    /// </remarks>
    Task StartPollingAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default);
}
