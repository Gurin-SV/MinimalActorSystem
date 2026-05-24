namespace MinimalActorSystem.Network;

/// <summary>
/// Базовый класс для proxy-акторов, обеспечивающих сетевое взаимодействие между узлами.
/// Наследуется от <see cref="Actor"/> и предоставляет функциональность для:
/// - Сериализации/десериализации писем
/// - Отправки сетевых писем через транспортный уровень
/// - Управления очередью исходящих писем при отсутствии подключения к маршрутизатору
/// - Регистрации таймаутов для операций, требующих ответа
/// </summary>
/// <remarks>
/// Base class for proxy actors that enable network communication between nodes.
/// Inherits from <see cref="Actor"/> and provides functionality for:
/// - Letter serialization/deserialization
/// - Sending network letters via transport layer
/// - Managing outgoing letter queue when router connection is unavailable
/// - Registering timeouts for operations requiring response
/// </remarks>
public abstract class NetworkProxyActor : Actor
{
    /// <summary>
    /// Очередь исходящих сетевых писем, ожидающих разрешения адреса или подключения.
    /// </summary>
    private readonly Queue<Letter> _pendingOutgoingLetters = new();

    /// <summary>
    /// Таблица разрешения имён узлов в сетевые адреса. Заполняется маршрутизатором.
    /// Ключ: символическое имя узла, Значение: сетевой адрес (URL).
    /// </summary>
    protected readonly Dictionary<string, string> NodeResolutionTable = [];

    /// <summary>
    /// Флаг доступности подключения к маршрутизатору.
    /// </summary>
    protected bool IsRouterConnected { get; set; }

    /// <summary>
    /// Настройки сети.
    /// </summary>
    protected NetworkSettings NetworkSettings => (NetworkSettings)System.Settings;

    /// <summary>
    /// Сериализатор для писем. Может быть переопределён в наследниках.
    /// </summary>
    protected virtual JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Создаёт proxy-актор с указанными параметрами.
    /// </summary>
    /// <param name="system">Акторная система.</param>
    /// <param name="uid">Уникальный идентификатор актора (должен быть <see cref="SystemUids.Network"/>).</param>
    /// <param name="name">Имя актора.</param>
    /// <param name="queueCapacity">Размер очереди сообщений.</param>
    protected NetworkProxyActor(IActorSystem system, Guid uid, string name, int queueCapacity = Actor.DefaultQueueCapacity)
        : base(system, uid, name, queueCapacity)
    {
        if (uid != SystemUids.Network)
            throw new ArgumentException($"Network proxy actor must have UID {SystemUids.Network}", nameof(uid));
    }

    /// <summary>
    /// Обрабатывает входящее письмо. Если письмо адресовано этому актору и содержит
    /// свойство ReceiverNodeName, оно преобразуется в сетевое письмо и отправляется по сети.
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    protected override async ValueTask OnLetter(Letter letter)
    {
        // Проверяем, что письмо предназначено для сетевого взаимодействия
        if (letter.Receiver == SystemUids.Network)
        {
            // Используем рефлексию для получения ReceiverNodeName (конвенция)
            var receiverNodeNameProperty = letter.GetType().GetProperty(nameof(INetworkLetterReceiver.ReceiverNodeName));
            if (receiverNodeNameProperty != null && receiverNodeNameProperty.GetValue(letter) is string receiverNodeName && !string.IsNullOrEmpty(receiverNodeName))
            {
                await SendToNetwork(letter, receiverNodeName);
                return;
            }
        }

        // Письмо получено из сети (уже десериализовано) - отправляем локальному актору
        if (letter.Receiver != SystemUids.Network && letter.Receiver != SystemUids.System && letter.Receiver != SystemUids.TimeService)
        {
            System.Send(letter);
            return;
        }

        // Обработка системных писем или писем без ReceiverNodeName
        await HandleLocalLetter(letter);
    }

    /// <summary>
    /// Отправляет письмо в сеть. Сериализует письмо, определяет адрес получателя через таблицу разрешения
    /// или запрашивает у маршрутизатора, затем отправляет через транспортный уровень.
    /// </summary>
    /// <param name="letter">Оригинальное письмо для отправки.</param>
    /// <param name="receiverNodeName">Имя узла-получателя.</param>
    /// <returns>Задача, представляющая асинхронную операцию отправки.</returns>
    protected abstract Task SendToNetwork(Letter letter, string receiverNodeName);

    /// <summary>
    /// Обрабатывает локальное письмо (не предназначенное для отправки в сеть).
    /// По умолчанию вызывает базовую реализацию <see cref="Actor.OnLetter"/>, но может быть переопределено.
    /// </summary>
    /// <param name="letter">Локальное письмо.</param>
    /// <returns>Задача, представляющая асинхронную операцию обработки.</returns>
    protected virtual ValueTask HandleLocalLetter(Letter letter) => default;

    /// <summary>
    /// Сериализует письмо в JSON.
    /// </summary>
    /// <param name="letter">Письмо для сериализации.</param>
    /// <returns>JSON-строка с сериализованным письмом.</returns>
    protected string SerializeLetter(Letter letter)
    {
        return JsonSerializer.Serialize(letter, letter.GetType(), JsonSerializerOptions);
    }

    /// <summary>
    /// Десериализует письмо из JSON.
    /// </summary>
    /// <param name="serializedLetter">JSON-строка с сериализованным письмом.</param>
    /// <param name="letterTypeName">Полное имя типа письма.</param>
    /// <returns>Десериализованное письмо.</returns>
    protected Letter DeserializeLetter(string serializedLetter, string letterTypeName)
    {
        var type = Type.GetType(letterTypeName, throwOnError: false)
            ?? throw new InvalidOperationException($"Type '{letterTypeName}' not found");

        return (Letter?)JsonSerializer.Deserialize(serializedLetter, type, JsonSerializerOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize letter of type '{letterTypeName}'");
    }

    /// <summary>
    /// Добавляет письмо в очередь ожидающих отправки. Вызывается, когда маршрут к получателю ещё не известен.
    /// </summary>
    /// <param name="letter">Письмо для постановки в очередь.</param>
    protected void EnqueuePending(Letter letter)
    {
        _pendingOutgoingLetters.Enqueue(letter);
    }

    /// <summary>
    /// Обрабатывает все отложенные письма после получения таблицы разрешения от маршрутизатора.
    /// </summary>
    /// <returns>Задача, представляющая асинхронную операцию обработки.</returns>
    protected async Task FlushPendingLetters()
    {
        while (_pendingOutgoingLetters.Count > 0)
        {
            var letter = _pendingOutgoingLetters.Dequeue();

            // Получаем ReceiverNodeName через рефлексию
            var receiverNodeNameProperty = letter.GetType().GetProperty(nameof(INetworkLetterReceiver.ReceiverNodeName));
            if (receiverNodeNameProperty != null && receiverNodeNameProperty.GetValue(letter) is string receiverNodeName)
            {
                await SendToNetwork(letter, receiverNodeName);
            }
        }
    }

    /// <summary>
    /// Обновляет таблицу разрешения имён узлов. Вызывается при получении данных от маршрутизатора.
    /// </summary>
    /// <param name="resolutionTable">Словарь: имя узла -> сетевой адрес.</param>
    protected void UpdateResolutionTable(Dictionary<string, string> resolutionTable)
    {
        foreach (var kvp in resolutionTable)
        {
            NodeResolutionTable[kvp.Key] = kvp.Value;
        }
    }
}
