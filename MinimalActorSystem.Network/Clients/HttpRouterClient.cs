using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network;

/// <summary>
/// Базовая реализация клиента маршрутизатора, использующая HTTP/REST.
/// Конкретная реализация может быть заменена на другую (gRPC, TCP, etc.) без изменения интерфейса.
/// </summary>
/// <remarks>
/// Base router client implementation using HTTP/REST.
/// Concrete implementation can be replaced with another (gRPC, TCP, etc.) without changing the interface.
/// </remarks>
/// <remarks>
/// Создаёт HTTP-клиент для работы с маршрутизатором.
/// </remarks>
/// <param name="httpClient">HTTP-клиент с настроенным BaseAddress.</param>
/// <param name="settings">Настройки сети.</param>
/// <param name="logger">Опциональный логгер.</param>
public class HttpRouterClient(HttpClient httpClient, NetworkSettings settings, ILogger? logger = null) : IRouterClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly NetworkSettings _settings = settings;
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Регистрирует узел на маршрутизаторе.
    /// </summary>
    public async Task<Dictionary<string, string>> RegisterNodeAsync(string nodeName, string nodeAddress,
        CancellationToken cancellationToken = default)
    {
        var request = new RegisterNodeRequest
        {
            NodeName = nodeName,
            NodeAddress = nodeAddress
        };

        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/router/register", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(responseString)
            ?? throw new InvalidOperationException("Failed to deserialize resolution table");
    }

    /// <summary>
    /// Получает таблицу разрешения имён.
    /// </summary>
    public async Task<Dictionary<string, string>> GetResolutionTableAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/router/resolution-table", cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(responseString)
            ?? throw new InvalidOperationException("Failed to deserialize resolution table");
    }

    /// <summary>
    /// Отправляет heartbeat маршрутизатору.
    /// </summary>
    public async Task<bool> SendHeartbeatAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var request = new HeartbeatRequest { NodeName = nodeName };
        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/router/heartbeat", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send heartbeat for node {NodeName}", nodeName);
            return false;
        }
    }
}
