using Microsoft.Extensions.Logging;
using System.IO;

namespace MinimalActorSystem.Network;

/// <summary>
/// Базовая реализация транспорта через HTTP/REST.
/// Для отправки использует POST-запросы, для приёма предоставляет метод десериализации.
/// Приложение само управляет HTTP-сервером и вызывает этот метод при получении запроса.
/// </summary>
/// <remarks>
/// Base transport implementation using HTTP/REST.
/// Uses POST requests for sending, and provides a deserialization method for receiving.
/// The application manages the HTTP server itself and calls this method upon receiving a request.
/// </remarks>
/// <remarks>
/// Создаёт HTTP-транспорт.
/// </remarks>
/// <param name="httpClient">HTTP-клиент для исходящих запросов.</param>
/// <param name="logger">Опциональный логгер.</param>
public class HttpNetworkTransport(HttpClient httpClient, ILogger? logger = null) : INetworkTransport
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Отправляет сетевое письмо через HTTP POST.
    /// </summary>
    public async Task SendAsync(string targetAddress, NetworkLetter networkLetter, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(networkLetter),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            // Формируем URL для отправки письма
            var url = $"{targetAddress}/api/node/receive";
            await _httpClient.PostAsync(url, content, cancellationToken);

            _logger?.LogDebug("Sent network letter to {TargetAddress}", targetAddress);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send network letter to {TargetAddress}", targetAddress);
            // Семантика "послал и забыл" - не выбрасываем исключение
        }
    }

    /// <summary>
    /// Десериализует входящий JSON-поток в сетевое письмо.
    /// Если поток поддерживает поиск (CanSeek), его позиция сбрасывается в начало перед чтением.
    /// Возвращает null, если поток пуст.
    /// Бросает исключения (например, JsonException) при ошибках формата JSON или чтения.
    /// </summary>
    public Task<NetworkLetter?> DeserializeAsync(Stream bodyStream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (bodyStream == null)
            throw new ArgumentNullException(nameof(bodyStream));

        if (bodyStream.CanSeek)
        {
            bodyStream.Position = 0;
            if (bodyStream.Length == 0)
            {
                return Task.FromResult<NetworkLetter?>(null);
            }
        }

        var result = JsonSerializer.Deserialize<NetworkLetter>(bodyStream);
        return Task.FromResult(result);
    }
}
