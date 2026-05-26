namespace MinimalActorSystem.Network;

/// <summary>
/// Информация об узле сети.
/// </summary>
/// <remarks>
/// Network node information.
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр информации об узле.
/// </remarks>
public sealed class NodeInfo(string nodeName, string nodeAddress)
{
    /// <summary>
    /// Имя узла.
    /// </summary>
    public string NodeName { get; } = nodeName ?? throw new ArgumentNullException(nameof(nodeName));

    /// <summary>
    /// Сетевой адрес узла.
    /// </summary>
    public string NodeAddress { get; set; } = nodeAddress ?? throw new ArgumentNullException(nameof(nodeAddress));

    /// <summary>
    /// Статус узла.
    /// </summary>
    public NodeStatus Status { get; set; } = NodeStatus.Inactive;

    /// <summary>
    /// Время последнего подтверждения активности.
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время регистрации узла.
    /// </summary>
    public DateTime RegisteredAt { get; } = DateTime.UtcNow;
}
