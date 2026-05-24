namespace MinimalActorSystem.Network;

/// <summary>
/// Запрос на регистрацию узла.
/// </summary>
internal sealed class RegisterNodeRequest
{
    public string NodeName { get; init; } = string.Empty;
    public string NodeAddress { get; init; } = string.Empty;
}
