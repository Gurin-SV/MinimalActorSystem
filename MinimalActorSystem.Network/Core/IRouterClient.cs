namespace MinimalActorSystem.Network;

/// <summary>
/// Клиент для взаимодействия с сервером-маршрутизатором. Предоставляет методы для:
/// - Регистрации узла в сети
/// - Получения таблицы разрешения имён узлов
/// - Подтверждения доступности узла (heartbeat)
/// </summary>
/// <remarks>
/// Client for interacting with the router server. Provides methods for:
/// - Registering a node in the network
/// - Getting the node name resolution table
/// - Confirming node availability (heartbeat)
/// </remarks>
public interface IRouterClient
{
    /// <summary>
    /// Регистрирует текущий узел на маршрутизаторе и получает таблицу разрешения имён.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла.</param>
    /// <param name="nodeAddress">Сетевой адрес узла для обратного подключения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Таблица разрешения: имя узла -> сетевой адрес.</returns>
    Task<Dictionary<string, string>> RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Запрашивает актуальную таблицу разрешения имён у маршрутизатора.
    /// </summary>
    /// <returns>Таблица разрешения: имя узла -> сетевой адрес.</returns>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<Dictionary<string, string>> GetResolutionTableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Подтверждает доступность узла (heartbeat). Вызывается периодически.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><c>true</c>, если маршрутизатор подтвердил получение; <c>false</c> в противном случае.</returns>
    Task<bool> SendHeartbeatAsync(string nodeName, CancellationToken cancellationToken = default);
}
