namespace MinimalActorSystem.Network;

/// <summary>
/// Базовый класс сетевых настроек. Наследуется от <see cref="Settings"/> и добавляет
/// конфигурацию для сетевого взаимодействия: адреса маршрутизаторов, таймауты, настройки сериализации.
/// </summary>
/// <remarks>
/// Network settings class. Extends <see cref="Settings"/> with network configuration:
/// router addresses, timeouts, serialization settings.
/// </remarks>
public class NetworkSettings : Settings
{
    /// <summary>
    /// Массив адресов серверов-маршрутизаторов. Узел пытается подключиться к первому доступному.
    /// Формат: "http://host:port" или "https://host:port".
    /// </summary>
    /// <remarks>
    /// Array of router server addresses. Node tries to connect to the first available one.
    /// Format: "http://host:port" or "https://host:port".
    /// </remarks>
    public string[] RouterAddresses { get; init; } = [];

    /// <summary>
    /// Символическое имя текущего узла. Должно быть уникальным в пределах сети.
    /// </summary>
    /// <remarks>
    /// Symbolic name of the current node. Must be unique within the network.
    /// </remarks>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// Таймаут запроса к маршрутизатору (в миллисекундах). По умолчанию 5000 мс.
    /// </summary>
    /// <remarks>
    /// Router request timeout in milliseconds. Default is 5000 ms.
    /// </remarks>
    public int RouterTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Интервал опроса маршрутизатором доступности узлов (в миллисекундах). По умолчанию 30000 мс.
    /// Применяется на стороне маршрутизатора.
    /// </summary>
    /// <remarks>
    /// Interval for router to poll node availability in milliseconds. Default is 30000 ms.
    /// Used on the router side.
    /// </remarks>
    public int NodePollIntervalMs { get; init; } = 30000;

    /// <summary>
    /// Таймаут ожидания ответа при сетевом взаимодействии (в миллисекундах). По умолчанию 10000 мс.
    /// </summary>
    /// <remarks>
    /// Timeout for waiting a response in network communication in milliseconds. Default is 10000 ms.
    /// </remarks>
    public int NetworkResponseTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// Максимальное количество повторных попыток подключения к маршрутизатору. По умолчанию 3.
    /// </summary>
    /// <remarks>
    /// Maximum retry attempts for connecting to a router. Default is 3.
    /// </remarks>
    public int MaxRouterRetryCount { get; init; } = 3;

    /// <summary>
    /// Интервал между повторными попытками подключения к маршрутизатору (в миллисекундах). По умолчанию 1000 мс.
    /// </summary>
    /// <remarks>
    /// Delay between retry attempts for router connection in milliseconds. Default is 1000 ms.
    /// </remarks>
    public int RouterRetryDelayMs { get; init; } = 1000;
}
