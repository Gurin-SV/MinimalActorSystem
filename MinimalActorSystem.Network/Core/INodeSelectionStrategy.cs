namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс стратегии выбора узла из списка доступных.
/// </summary>
/// <remarks>
/// Interface for node selection strategy from available nodes list.
/// </remarks>
public interface INodeSelectionStrategy
{
    /// <summary>
    /// Выбирает узел из списка доступных.
    /// </summary>
    /// <param name="availableNodes">Список имён доступных узлов.</param>
    /// <param name="nodeRegistry">Реестр узлов для получения дополнительной информации.</param>
    /// <param name="currentNodeName">Имя текущего узла (не должен выбирать себя).</param>
    /// <returns>Выбранное имя узла или null, если выбор невозможен.</returns>
    string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName);
}
