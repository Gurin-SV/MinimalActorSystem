using System.Collections.Concurrent;
using System.Linq;

namespace MinimalActorSystem.Network;

/// <summary>
/// Локальный реестр узлов сети.
/// Хранит информацию об известных узлах, их адресах и статусах.
/// </summary>
/// <remarks>
/// Local registry of network nodes.
/// Stores information about known nodes, their addresses and statuses.
/// </remarks>
public sealed class NodeRegistry
{
    private readonly ConcurrentDictionary<string, NodeInfo> _nodes = new();

    /// <summary>
    /// Количество зарегистрированных узлов.
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// Регистрирует или обновляет информацию об узле.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <param name="nodeAddress">Сетевой адрес узла.</param>
    /// <returns>true, если узел новый, false если обновлён существующий.</returns>
    public bool RegisterNode(string nodeName, string nodeAddress)
    {
        var isNew = !_nodes.ContainsKey(nodeName);
        _nodes[nodeName] = new NodeInfo(nodeName, nodeAddress);
        return isNew;
    }

    /// <summary>
    /// Удаляет узел из реестра.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <returns>true, если узел был удалён, иначе false.</returns>
    public bool UnregisterNode(string nodeName)
    {
        return _nodes.TryRemove(nodeName, out _);
    }

    /// <summary>
    /// Пытается получить сетевой адрес узла.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <param name="nodeAddress">Сетевой адрес (если найден).</param>
    /// <returns>true, если узел найден, иначе false.</returns>
    public bool TryGetNodeAddress(string nodeName, out string? nodeAddress)
    {
        if (_nodes.TryGetValue(nodeName, out var nodeInfo))
        {
            nodeAddress = nodeInfo.NodeAddress;
            return true;
        }

        nodeAddress = null;
        return false;
    }

    /// <summary>
    /// Получает информацию об узле.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <returns>Информация об узле или null, если узел не найден.</returns>
    public NodeInfo? GetNodeInfo(string nodeName)
    {
        return _nodes.TryGetValue(nodeName, out var nodeInfo) ? nodeInfo : null;
    }

    /// <summary>
    /// Обновляет статус узла.
    /// </summary>
    /// <param name="nodeName">Имя узла.</param>
    /// <param name="status">Новый статус.</param>
    /// <returns>true, если узел найден и обновлён, иначе false.</returns>
    public bool SetNodeStatus(string nodeName, NodeStatus status)
    {
        if (_nodes.TryGetValue(nodeName, out var nodeInfo))
        {
            nodeInfo.Status = status;
            nodeInfo.LastSeen = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает все зарегистрированные узлы.
    /// </summary>
    public IReadOnlyList<NodeInfo> GetAllNodes()
    {
        return [.. _nodes.Values];
    }

    /// <summary>
    /// Возвращает все активные узлы.
    /// </summary>
    public IReadOnlyList<NodeInfo> GetActiveNodes()
    {
        return [.. _nodes.Values.Where(n => n.Status == NodeStatus.Active)];
    }

    /// <summary>
    /// Возвращает все неактивные узлы.
    /// </summary>
    public IReadOnlyList<NodeInfo> GetInactiveNodes()
    {
        return [.. _nodes.Values.Where(n => n.Status == NodeStatus.Inactive)];
    }

    /// <summary>
    /// Проверяет, зарегистрирован ли узел.
    /// </summary>
    public bool Contains(string nodeName)
    {
        return _nodes.ContainsKey(nodeName);
    }

    /// <summary>
    /// Очищает реестр.
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
    }
}
