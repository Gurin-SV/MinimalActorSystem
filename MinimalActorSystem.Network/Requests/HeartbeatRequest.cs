namespace MinimalActorSystem.Network;

/// <summary>
/// Запрос heartbeat.
/// </summary>
internal sealed class HeartbeatRequest
{
    public string NodeName { get; init; } = string.Empty;
}
