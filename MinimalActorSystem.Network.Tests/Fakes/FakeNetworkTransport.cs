using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network.Tests.Fakes;

/// <summary>
/// Фейковая реализация INetworkTransport для тестирования.
/// Не требует реального сетевого подключения и работает в памяти.
/// </summary>
/// <remarks>
/// Fake implementation of INetworkTransport for testing.
/// Does not require real network connection and works in memory.
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр фейкового транспорта с указанным реестром узлов.
/// </remarks>
/// <param name="nodeRegistry">Реестр узлов (разделяемый между всеми транспортами в тесте).</param>
/// <param name="system">Акторная система для логгирования.</param>
/// <remarks>
/// Creates a new instance of fake transport with the specified node registry.
/// </remarks>
public sealed class FakeNetworkTransport(
    ConcurrentDictionary<string, FakeNetworkTransport> nodeRegistry,
    IActorSystem system)
    : INetworkTransport
{
    private readonly ConcurrentDictionary<string, FakeNetworkTransport> _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
    private readonly ILogger _logger = system.Logger;
    private Func<string, string, Task>? _onMessageReceived;
    private bool _disposed;

    /// <summary>
    /// Имя текущего узла.
    /// </summary>
    public string CurrentNodeName { get; private set; } = string.Empty;

    /// <summary>
    /// Зарегистрирован ли узел.
    /// </summary>
    public bool IsRegistered { get; private set; }

    private void LogDebug(string message)
    {
        _logger.LogDebug("[FakeTransport] {Message}", message);
    }

    private void LogWarning(string message)
    {
        _logger.LogWarning("[FakeTransport] {Message}", message);
    }

    /// <inheritdoc/>
    public void SetMessageHandler(Func<string, string, Task> onMessageReceived)
    {
        CheckDisposed();
        _onMessageReceived = onMessageReceived ?? throw new ArgumentNullException(nameof(onMessageReceived));
        LogDebug("Обработчик сообщений установлен");
    }

    /// <inheritdoc/>
    public Task RegisterNodeAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        CurrentNodeName = nodeName;
        IsRegistered = true;

        _nodeRegistry[nodeName] = this;
        LogDebug($"Узел '{nodeName}' зарегистрирован");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendAsync(string nodeName, string serializedMessage, CancellationToken cancellationToken = default)
    {
        CheckDisposed();

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        if (!IsRegistered)
        {
            LogWarning($"Sender '{CurrentNodeName}' is not registered");
            throw new InvalidOperationException($"Node {CurrentNodeName} is not registered");
        }

        if (!_nodeRegistry.TryGetValue(nodeName, out var targetTransport))
        {
            LogWarning($"Node '{nodeName}' not found in registry");
            throw new InvalidOperationException($"Node {nodeName} not found");
        }

        LogDebug($"Sending message from '{CurrentNodeName}' to '{nodeName}'");

        _ = Task.Run(async () =>
        {
            await Task.Delay(10, cancellationToken);
            LogDebug($"Delivering message to node '{nodeName}'");
            await targetTransport.DeliverMessageAsync(CurrentNodeName, serializedMessage);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Доставляет сообщение локальному обработчику.
    /// </summary>
    private async Task DeliverMessageAsync(string sourceNodeName, string serializedMessage)
    {
        LogDebug($"Получено сообщение от '{sourceNodeName}'");

        if (_onMessageReceived != null)
        {
            await _onMessageReceived(sourceNodeName, serializedMessage);
        }
        else
        {
            LogWarning("Обработчик сообщений не установлен");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (!string.IsNullOrEmpty(CurrentNodeName))
        {
            _nodeRegistry.TryRemove(CurrentNodeName, out _);
        }

        _disposed = true;
    }

    private void CheckDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
