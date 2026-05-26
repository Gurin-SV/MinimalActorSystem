namespace MinimalActorSystem.Network.Tests.Actors;

/// <summary>
/// Идентификаторы акторов для тестового узла.
/// </summary>
/// <remarks>
/// Actor UIDs for test node.
/// </remarks>
public static class ComputeNodeUids
{
    /// <summary>
    /// Идентификатор актора для вычислений.
    /// </summary>
    public static readonly Guid PrimeCalculator = Guid.NewGuid();

    /// <summary>
    /// Идентификатор актора для сохранения данных.
    /// </summary>
    public static readonly Guid DataSaver = Guid.NewGuid();
}
