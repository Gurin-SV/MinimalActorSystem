using System.Linq;

namespace MinimalActorSystem.Network;

/// <summary>
/// Стратегия выбора первого доступного узла.
/// </summary>
/// <remarks>
/// First available node selection strategy.
/// </remarks>
public sealed class FirstAvailableStrategy : INodeSelectionStrategy
{
    /// <inheritdoc/>
    public string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName)
    {
        if (availableNodes == null || availableNodes.Length == 0)
            return null;

        foreach (var node in availableNodes)
        {
            if (node == currentNodeName)
                continue;

            var nodeInfo = nodeRegistry.GetNodeInfo(node);
            if (nodeInfo != null && nodeInfo.Status == NodeStatus.Active)
            {
                return node;
            }
        }

        return availableNodes.FirstOrDefault(n => n != currentNodeName);
    }
}

/// <summary>
/// Стратегия выбора узла с наименьшей нагрузкой (по количеству отложенных сообщений).
/// </summary>
/// <remarks>
/// Least loaded node selection strategy (based on pending messages count).
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр стратегии.
/// </remarks>
/// <param name="pendingQueue">Очередь отложенных сообщений для получения информации о нагрузке.</param>
public sealed class LeastLoadedStrategy(IPendingMessageQueue pendingQueue) : INodeSelectionStrategy
{
    private readonly IPendingMessageQueue _pendingQueue = pendingQueue 
        ?? throw new ArgumentNullException(nameof(pendingQueue));

    /// <inheritdoc/>
    public string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName)
    {
        if (availableNodes == null || availableNodes.Length == 0)
            return null;

        string? selectedNode = null;
        var minLoad = int.MaxValue;

        foreach (var node in availableNodes)
        {
            if (node == currentNodeName)
                continue;

            var nodeInfo = nodeRegistry.GetNodeInfo(node);
            if (nodeInfo == null || nodeInfo.Status != NodeStatus.Active)
                continue;

            var load = _pendingQueue.GetQueuedCountForNode(node);
            if (load < minLoad)
            {
                minLoad = load;
                selectedNode = node;
            }
        }

        return selectedNode ?? availableNodes.FirstOrDefault(n => n != currentNodeName);
    }
}

/// <summary>
/// Стратегия выбора узла по круговому циклическому перебору (round-robin).
/// </summary>
/// <remarks>
/// Round-robin node selection strategy.
/// </remarks>
public sealed class RoundRobinStrategy : INodeSelectionStrategy
{
    private readonly object _lock = new();
    private int _currentIndex;

    /// <inheritdoc/>
    public string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName)
    {
        if (availableNodes == null || availableNodes.Length == 0)
            return null;

        var activeNodes = availableNodes
            .Where(node =>
                node != currentNodeName &&
                nodeRegistry.GetNodeInfo(node)?.Status == NodeStatus.Active)
            .ToArray();

        if (activeNodes.Length == 0)
            return availableNodes.FirstOrDefault(n => n != currentNodeName);

        lock (_lock)
        {
            var selected = activeNodes[_currentIndex % activeNodes.Length];
            _currentIndex++;
            return selected;
        }
    }
}
