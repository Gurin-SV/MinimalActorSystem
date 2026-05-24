using System.Collections.Concurrent;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Эмулятор сервера-маршрутизатора (Web Service).
/// Хранит состояние топологии и отвечает на запросы клиентов.
/// </summary>
public class FakeRouterService
{
    // Таблица узлов: Имя узла -> Адрес
    private readonly ConcurrentDictionary<string, string> _nodes = new();

    // Флаг для эмуляции недоступности сервиса
    public bool IsDown { get; set; } = false;

    // Задержка для эмуляции сети (опционально)
    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Регистрация узла (аналог PUT /register/{name}?address={addr})
    /// </summary>
    public Task<bool> RegisterNodeAsync(string nodeName, string address, CancellationToken cancellationToken = default)
    {
        if (IsDown) return Task.FromResult(false);

        SimulateLatency();
        cancellationToken.ThrowIfCancellationRequested();

        // Добавляем или обновляем запись
        _nodes.AddOrUpdate(nodeName, address, (_, _) => address);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Получение всей таблицы маршрутизации (аналог GET /topology)
    /// </summary>
    public Task<Dictionary<string, string>> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        if (IsDown) throw new InvalidOperationException("Service is down");

        SimulateLatency();
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_nodes.ToDictionary(k => k.Key, v => v.Value));
    }

    /// <summary>
    /// Heartbeat (аналог PUT /heartbeat/{name})
    /// </summary>
    public Task<bool> HeartbeatAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        if (IsDown) return Task.FromResult(false);

        // Если узел не зарегистрирован, heartbeat может вернуть ошибку или игнорироваться
        // В данной реализации просто проверяем наличие
        if (!_nodes.ContainsKey(nodeName))
        {
            return Task.FromResult(false);
        }

        SimulateLatency();
        cancellationToken.ThrowIfCancellationRequested();

        // Обновляем время жизни (в реальной БД), здесь просто факт существования
        return Task.FromResult(true);
    }

    /// <summary>
    /// Удаление узла (для тестов очистки)
    /// </summary>
    public void RemoveNode(string nodeName)
    {
        _nodes.TryRemove(nodeName, out _);
    }

    /// <summary>
    /// Очистка всего состояния
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        IsDown = false;
        Latency = TimeSpan.Zero;
    }

    private void SimulateLatency()
    {
        if (Latency > TimeSpan.Zero)
        {
            Thread.Sleep(Latency);
        }
    }
}
