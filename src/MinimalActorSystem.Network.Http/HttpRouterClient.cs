using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network.Http;

/// <summary>
/// HTTP реализация клиента роутера.
/// Взаимодействует с HTTP-сервисом роутера для регистрации узлов и разрешения имён.
/// </summary>
/// <remarks>
/// HTTP implementation of router client.
/// Communicates with HTTP router service for node registration and name resolution.
/// </remarks>
public sealed class HttpRouterClient(
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    IEnumerable<string> routerAddresses) : IRouterClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly List<string> _routerAddresses = new(routerAddresses ?? throw new ArgumentNullException(nameof(routerAddresses)));
    private readonly object _lock = new();
    private Func<string, string, Task>? _onNodeDiscovered;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private bool _disposed;
    private string? _nodeName;
    private string? _nodeAddress;

    private void CheckDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HttpRouterClient));
    }

    private async Task<T?> TrySendToRouterAsync<T>(
        Func<string, HttpClient, Task<T?>> sendAction,
        CancellationToken cancellationToken) where T : class
    {
        foreach (var routerAddress in _routerAddresses)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var result = await sendAction(routerAddress, httpClient);
                if (result != null)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HttpRouterClient: failed to communicate with router {RouterAddress}", routerAddress);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        if (string.IsNullOrWhiteSpace(nodeAddress))
            throw new ArgumentException("Node address cannot be empty", nameof(nodeAddress));

        _nodeName = nodeName;
        _nodeAddress = nodeAddress;

        var request = new RegisterNodeRequest
        {
            NodeName = nodeName,
            NodeAddress = nodeAddress
        };

        var result = await TrySendToRouterAsync<RegisterNodeResponse>(async (routerAddress, httpClient) =>
        {
            var url = $"{routerAddress}/api/router/register";
            var response = await httpClient.PostAsJsonAsync(url, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HttpRouterClient: register failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RegisterNodeResponse>(cancellationToken);
        }, cancellationToken);

        if (result == null || !result.Success)
        {
            _logger.LogError("HttpRouterClient: failed to register node {NodeName}", nodeName);
            throw new InvalidOperationException($"Failed to register node {nodeName}");
        }

        _logger.LogInformation("HttpRouterClient: node {NodeName} registered successfully", nodeName);
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveNodeAddressAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        var request = new ResolveNodeRequest { NodeName = nodeName };

        var result = await TrySendToRouterAsync<ResolveNodeResponse>(async (routerAddress, httpClient) =>
        {
            var url = $"{routerAddress}/api/router/resolve";
            var response = await httpClient.PostAsJsonAsync(url, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ResolveNodeResponse>(cancellationToken);
        }, cancellationToken);

        if (result == null || !result.Found)
        {
            _logger.LogDebug("HttpRouterClient: node {NodeName} not found", nodeName);
            return null;
        }

        _logger.LogDebug("HttpRouterClient: resolved node {NodeName} -> {NodeAddress}", nodeName, result.NodeAddress);
        return result.NodeAddress;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        var result = await TrySendToRouterAsync<GetAllNodesResponse>(async (routerAddress, httpClient) =>
        {
            var url = $"{routerAddress}/api/router/nodes";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<GetAllNodesResponse>(cancellationToken);
        }, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("HttpRouterClient: failed to get all nodes");
            return new Dictionary<string, string>();
        }

        _logger.LogDebug("HttpRouterClient: retrieved {Count} nodes", result.Nodes.Count);
        return result.Nodes;
    }

    /// <inheritdoc/>
    public void SetNodeDiscoveredHandler(Func<string, string, Task> onNodeDiscovered)
    {
        CheckDisposed();
        _onNodeDiscovered = onNodeDiscovered ?? throw new ArgumentNullException(nameof(onNodeDiscovered));
        _logger.LogDebug("HttpRouterClient: node discovered handler set");
    }

    /// <inheritdoc/>
    public async Task StartPollingAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (pollingInterval <= TimeSpan.Zero)
            throw new ArgumentException("Polling interval must be positive", nameof(pollingInterval));

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => PollingLoopAsync(pollingInterval, _pollingCts.Token), _pollingCts.Token);

        _logger.LogInformation("HttpRouterClient: started polling with interval {Interval} ms", pollingInterval.TotalMilliseconds);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Фоновый цикл опроса роутера.
    /// </summary>
    /// <remarks>
    /// Background polling loop.
    /// </remarks>
    private async Task PollingLoopAsync(TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        var lastKnownNodes = new Dictionary<string, string>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(pollingInterval, cancellationToken);

                var currentNodes = await GetAllNodesAsync(cancellationToken);

                foreach (var node in currentNodes)
                {
                    if (!lastKnownNodes.ContainsKey(node.Key) && _onNodeDiscovered != null)
                    {
                        _logger.LogInformation("HttpRouterClient: discovered new node {NodeName}", node.Key);
                        await _onNodeDiscovered(node.Key, node.Value);
                    }
                }

                lastKnownNodes = new Dictionary<string, string>(currentNodes);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("HttpRouterClient: polling loop cancelled");
        }
    }

    /// <summary>
    /// Останавливает опрос роутера.
    /// </summary>
    /// <remarks>
    /// Stops router polling.
    /// </remarks>
    public async Task StopPollingAsync()
    {
        if (_pollingCts != null)
        {
            await _pollingCts.CancelAsync();
            _pollingCts.Dispose();
            _pollingCts = null;
        }

        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation
            }
            _pollingTask = null;
        }

        _logger.LogInformation("HttpRouterClient: stopped polling");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _pollingCts?.Cancel();
        _pollingCts?.Dispose();

        _disposed = true;
        _logger.LogDebug("HttpRouterClient: disposed");
    }
}
