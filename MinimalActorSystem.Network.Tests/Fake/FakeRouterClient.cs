using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Фейковая реализация клиента маршрутизатора для тестов.
/// Работает напрямую с FakeRouterService, эмулируя сетевые вызовы.
/// </summary>
public class FakeRouterClient(FakeRouterService service) : IRouterClient
{
    private readonly FakeRouterService _service = service ?? throw new ArgumentNullException(nameof(service));

    // Опционально: имя узла, которое этот клиент представляет
    private string? _currentNodeName;
    private string? _currentNodeAddress;

    /// <summary>
    /// Регистрирует узел в сервисе и возвращает таблицу маршрутизации.
    /// </summary>
    public async Task<Dictionary<string, string>> RegisterNodeAsync(string nodeName, string nodeAddress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _currentNodeName = nodeName;
        _currentNodeAddress = nodeAddress;

        // Вызываем метод сервиса
        var success = await _service.RegisterNodeAsync(nodeName, nodeAddress, cancellationToken);

        if (!success)
        {
            // В реальной ситуации это могло бы быть исключение или пустой словарь
            throw new InvalidOperationException("Failed to register node on router.");
        }

        // Сразу получаем таблицу после регистрации
        return await _service.GetTopologyAsync(cancellationToken);
    }

    /// <summary>
    /// Запрашивает актуальную таблицу маршрутизации.
    /// </summary>
    public async Task<Dictionary<string, string>> GetResolutionTableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _service.GetTopologyAsync(cancellationToken);
    }

    /// <summary>
    /// Отправляет heartbeat для зарегистрированного узла.
    /// </summary>
    public async Task<bool> SendHeartbeatAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _service.HeartbeatAsync(nodeName, cancellationToken);
    }
}
