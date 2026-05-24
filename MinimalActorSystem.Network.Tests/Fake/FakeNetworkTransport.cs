using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Фейковая реализация транспорта для тестирования в памяти.
/// Эмулирует отправку сообщений в "сеть" (внутреннюю очередь) и десериализацию.
/// </summary>
public class FakeNetworkTransport : INetworkTransport
{
    // Очередь входящих сообщений, которые "пришли из сети"
    private readonly Channel<NetworkLetter> _incomingChannel = Channel.CreateUnbounded<NetworkLetter>();

    // Журнал отправленных сообщений для/assert проверок
    private readonly ConcurrentBag<(string TargetNode, NetworkLetter Letter)> _sentMessages = [];

    /// <summary>
    /// Имитирует получение сообщения из внешней сети.
    /// Вызывается тестом или эмулятором сервера, чтобы "доставить" письмо в транспорт.
    /// </summary>
    public void SimulateIncomingMessage(NetworkLetter letter)
    {
        _incomingChannel.Writer.TryWrite(letter);
    }

    /// <summary>
    /// Отправляет письмо в "сеть". В фейке просто сохраняет его в журнал и возвращает успех.
    /// </summary>
    public Task SendAsync(string targetNodeUrl, NetworkLetter letter, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<bool>(cancellationToken);

        _sentMessages.Add((targetNodeUrl, letter));

        // В реальной реализации здесь был бы HTTP запрос.
        // Для фейка считаем, что доставка мгновенна и успешна.
        return Task.FromResult(true);
    }

    /// <summary>
    /// Десериализует входящий JSON-поток в сетевое письмо.
    /// Если поток поддерживает поиск (CanSeek), его позиция сбрасывается в начало перед чтением.
    /// Возвращает null, если поток пуст.
    /// Бросает исключения (например, JsonException) при ошибках формата JSON или чтения.
    /// </summary>
    public Task<NetworkLetter?> DeserializeAsync(Stream bodyStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bodyStream);

        cancellationToken.ThrowIfCancellationRequested();

        // Сброс позиции в начало, если поток поддерживает поиск
        if (bodyStream.CanSeek)
        {
            bodyStream.Position = 0;
        }

        // Проверка на пустой поток (после сброса позиции)
        if (bodyStream.CanSeek && bodyStream.Position == bodyStream.Length)
        {
            return Task.FromResult<NetworkLetter?>(null);
        }

        // Десериализуем в конкретный тип, так как NetworkLetter абстрактный
        // В будущем здесь может быть логика полиморфной десериализации
        var result = JsonSerializer.Deserialize<DefaultNetworkLetter>(bodyStream);

        return Task.FromResult<NetworkLetter?>(result);
    }

    /// <summary>
    /// Метод для чтения входящих сообщений в цикле (эмуляция Listen).
    /// Возвращает следующее доступное письмо или ждет его появления.
    /// </summary>
    public async Task<NetworkLetter?> ReadIncomingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _incomingChannel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Проверка: было ли отправлено сообщение на указанный узел.
    /// </summary>
    public bool WasSentTo(string nodeUrl)
    {
        return _sentMessages.Any(x => x.TargetNode == nodeUrl);
    }

    /// <summary>
    /// Получить все отправленные сообщения (для детальных проверок).
    /// </summary>
    public IEnumerable<(string TargetNode, NetworkLetter Letter)> GetSentMessages()
    {
        return [.. _sentMessages];
    }

    /// <summary>
    /// Очистить историю отправленных сообщений.
    /// </summary>
    public void ClearHistory()
    {
        _sentMessages.Clear();
    }
}
