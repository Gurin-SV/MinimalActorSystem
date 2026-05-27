using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MinimalActorSystem.Network.Tests.Fakes;

/// <summary>
/// Фейковая реализация IRouterClient для тестирования.
/// Работает в памяти, не требует реального HTTP-сервера.
/// </summary>
/// <remarks>
/// Fake implementation of IRouterClient for testing.
/// Works in memory, does not require a real HTTP server.
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр фейкового роутер-клиента.
/// </remarks>
/// <param name="system">Акторная система для логгирования.</param>
public sealed class FakeRouterClient(IActorSystem system) : IRouterClient
{
    private readonly ILogger _logger = system.Logger;
    private readonly ConcurrentDictionary<string, string> _nodes = new();
    private Func<string, string, Task>? _onNodeDiscovered;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private bool _disposed;

    /// <summary>
    /// Задержка, симулирующая сетевую задержку при регистрации и разрешении имён.
    /// По умолчанию 10 мс.
    /// </summary>
    public TimeSpan SimulatedNetworkDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    private async Task SimulateNetworkDelay(CancellationToken cancellationToken)
    {
        if (SimulatedNetworkDelay > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedNetworkDelay, cancellationToken);
        }
    }

    private void LogDebug(string message)
    {
        _logger.LogDebug("[FakeRouterClient] {Message}", message);
    }

    private void LogWarning(string message)
    {
        _logger.LogWarning("[FakeRouterClient] {Message}", message);
    }

    /// <inheritdoc/>
    public async Task RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));
        if (string.IsNullOrWhiteSpace(nodeAddress))
            throw new ArgumentException("Node address cannot be empty", nameof(nodeAddress));

        await SimulateNetworkDelay(cancellationToken);

        bool isNew = !_nodes.ContainsKey(nodeName);
        _nodes[nodeName] = nodeAddress;

        LogDebug($"Узел зарегистрирован: '{nodeName}' -> '{nodeAddress}' (новый: {isNew})");

        // Если это новый узел и установлен обработчик, уведомляем
        if (isNew && _onNodeDiscovered != null)
        {
            LogDebug($"Уведомление о новом узле: '{nodeName}'");
            await _onNodeDiscovered(nodeName, nodeAddress);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveNodeAddressAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        await SimulateNetworkDelay(cancellationToken);

        if (_nodes.TryGetValue(nodeName, out string? address))
        {
            LogDebug($"Разрешение имени: '{nodeName}' -> '{address}'");
            return address;
        }

        LogDebug($"Разрешение имени: '{nodeName}' -> не найден");
        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllNodesAsync(CancellationToken cancellationToken = default)
    {
        await SimulateNetworkDelay(cancellationToken);

        var snapshot = _nodes.ToDictionary(kv => kv.Key, kv => kv.Value);
        LogDebug($"Получен список всех узлов: {snapshot.Count} узлов");

        return snapshot;
    }

    /// <inheritdoc/>
    public void SetNodeDiscoveredHandler(Func<string, string, Task> onNodeDiscovered)
    {
        _onNodeDiscovered = onNodeDiscovered ?? throw new ArgumentNullException(nameof(onNodeDiscovered));
        LogDebug("Обработчик обнаружения узлов установлен");
    }

    /// <inheritdoc/>
    public Task StartPollingAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default)
    {
        if (pollingInterval <= TimeSpan.Zero)
            throw new ArgumentException("Polling interval must be positive", nameof(pollingInterval));

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = Task.Run(() => PollingLoopAsync(pollingInterval, _pollingCts.Token), _pollingCts.Token);

        LogDebug($"Запущен опрос роутера с интервалом {pollingInterval.TotalMilliseconds} мс");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Фоновый цикл опроса роутера.
    /// </summary>
    private async Task PollingLoopAsync(TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        LogDebug("Цикл опроса роутера запущен");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(pollingInterval, cancellationToken);

                // В фейковой реализации просто логируем факт опроса
                LogDebug($"Опрос роутера: {_nodes.Count} узлов в реестре");
            }
        }
        catch (OperationCanceledException)
        {
            LogDebug("Цикл опроса роутера остановлен");
        }
    }

    /// <summary>
    /// Добавляет узел в реестр вручную (для тестов) и вызывает уведомление.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <param name="nodeAddress">Сетевой адрес узла.</param>
    public async Task AddNodeForTestingAsync(string nodeName, string nodeAddress)
    {
        bool isNew = !_nodes.ContainsKey(nodeName);
        _nodes[nodeName] = nodeAddress;
        LogDebug($"Узел добавлен вручную для теста: '{nodeName}' -> '{nodeAddress}' (новый: {isNew})");

        if (isNew && _onNodeDiscovered != null)
        {
            LogDebug($"Уведомление о новом узле: '{nodeName}'");
            await _onNodeDiscovered(nodeName, nodeAddress);
        }
    }

    /// <summary>
    /// Очищает реестр узлов (для тестов).
    /// </summary>
    public void ClearNodesForTesting()
    {
        _nodes.Clear();
        LogDebug("Реестр узлов очищен");
    }

    /// <summary>
    /// Останавливает опрос роутера.
    /// </summary>
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
                // Ожидаемая отмена
            }
            _pollingTask = null;
        }

        LogDebug("Опрос роутера остановлен");
    }

    /// <inheritdoc/>
    public async void Dispose()
    {
        if (_disposed)
            return;

        await StopPollingAsync();
        _disposed = true;
    }
}
