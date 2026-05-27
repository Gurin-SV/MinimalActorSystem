using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace MinimalActorSystem.Network.Http;

/// <summary>
/// HTTP реализация транспортного уровня.
/// Отправляет сообщения на указанный узел через HTTP POST.
/// </summary>
/// <remarks>
/// HTTP implementation of the transport layer.
/// Sends messages to the specified node via HTTP POST.
/// </remarks>
public sealed class HttpNetworkTransport(
    IHttpClientFactory httpClientFactory,
    ILogger logger) : INetworkTransport
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private Func<string, string, Task>? _onMessageReceived;
    private string? _nodeName;
    private string? _nodeAddress;
    private bool _disposed;
    private CancellationTokenSource? _listeningCts;
    private Task? _listeningTask;

    private void CheckDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HttpNetworkTransport));
    }

    /// <inheritdoc/>
    public Task RegisterNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        _nodeName = nodeName;
        _logger.LogDebug("HttpNetworkTransport: node '{NodeName}' registered", nodeName);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void SetMessageHandler(Func<string, string, Task> onMessageReceived)
    {
        CheckDisposed();
        _onMessageReceived = onMessageReceived ?? throw new ArgumentNullException(nameof(onMessageReceived));
        _logger.LogDebug("HttpNetworkTransport: message handler set");
    }

    /// <summary>
    /// Запускает HTTP сервер для приёма входящих сообщений.
    /// </summary>
    /// <param name="nodeAddress">Адрес для прослушивания (например, "http://localhost:8080").</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>
    /// Starts HTTP server for receiving incoming messages.
    /// </remarks>
    public async Task StartListeningAsync(string nodeAddress, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeAddress))
            throw new ArgumentException("Node address cannot be empty", nameof(nodeAddress));

        if (_nodeName == null)
            throw new InvalidOperationException("Node must be registered before starting listening");

        _nodeAddress = nodeAddress;
        _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listeningTask = Task.Run(() => RunHttpServerAsync(_listeningCts.Token), _listeningCts.Token);
        _logger.LogInformation("HttpNetworkTransport: started listening on {NodeAddress}", nodeAddress);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Внутренний HTTP сервер для приёма сообщений.
    /// </summary>
    /// <remarks>
    /// Internal HTTP server for receiving messages.
    /// </remarks>
    private async Task RunHttpServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("HttpNetworkTransport: HTTP server implementation requires ASP.NET Core hosting.");
        _logger.LogWarning("Consider using WebApplication or integrating with existing host.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Обрабатывает входящее HTTP сообщение (вызывается из HTTP сервера).
    /// </summary>
    /// <param name="sourceNodeName">Имя узла-отправителя.</param>
    /// <param name="serializedMessage">Сериализованное сообщение.</param>
    /// <remarks>
    /// Processes incoming HTTP message (called from HTTP server).
    /// </remarks>
    internal async Task ProcessIncomingMessage(string sourceNodeName, string serializedMessage)
    {
        if (_onMessageReceived == null)
        {
            _logger.LogWarning("HttpNetworkTransport: received message but no handler set");
            return;
        }

        _logger.LogDebug("HttpNetworkTransport: processing message from {SourceNode}", sourceNodeName);
        await _onMessageReceived(sourceNodeName, serializedMessage);
    }

    /// <inheritdoc/>
    public async Task SendAsync(string nodeName, string serializedMessage, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        if (string.IsNullOrWhiteSpace(serializedMessage))
            throw new ArgumentException("Message cannot be empty", nameof(serializedMessage));

        if (_nodeName == null)
            throw new InvalidOperationException("Node must be registered before sending messages");

        SendMessageRequest request = new()
        {
            SourceNodeName = _nodeName,
            SerializedMessage = serializedMessage
        };

        string targetUrl = $"{nodeName}/api/message";

        _logger.LogDebug("HttpNetworkTransport: sending message to node '{NodeName}'", nodeName);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(targetUrl, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("HttpNetworkTransport: message sent successfully to {NodeName}", nodeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HttpNetworkTransport: failed to send message to {NodeName}", nodeName);
            throw;
        }
    }

    /// <summary>
    /// Останавливает прослушивание.
    /// </summary>
    /// <remarks>
    /// Stops listening.
    /// </remarks>
    public async Task StopListeningAsync()
    {
        if (_listeningCts != null)
        {
            await _listeningCts.CancelAsync();
            _listeningCts.Dispose();
            _listeningCts = null;
        }

        if (_listeningTask != null)
        {
            try
            {
                await _listeningTask;
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation
            }
            _listeningTask = null;
        }

        _logger.LogInformation("HttpNetworkTransport: stopped listening");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _listeningCts?.Cancel();
        _listeningCts?.Dispose();

        _disposed = true;
        _logger.LogDebug("HttpNetworkTransport: disposed");
    }
}
