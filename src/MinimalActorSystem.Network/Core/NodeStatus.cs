namespace MinimalActorSystem.Network;

/// <summary>
/// Статус узла сети.
/// </summary>
/// <remarks>
/// Network node status.
/// </remarks>
public enum NodeStatus
{
    /// <summary>
    /// Узел недоступен (неактивен).
    /// </summary>
    Inactive,

    /// <summary>
    /// Узел активен и доступен.
    /// </summary>
    Active
}
