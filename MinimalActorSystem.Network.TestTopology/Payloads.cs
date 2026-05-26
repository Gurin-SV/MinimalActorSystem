namespace MinimalActorSystem.Network.TestTopology;

/// <summary>
/// Payload для тестирования вычислений.
/// </summary>
/// <remarks>
/// Payload for computation testing.
/// </remarks>
[DestinationNode(TestNodeNames.ComputeNode)]
[DestinationNode(TestNodeNames.BackupComputeNode)]
public sealed class ComputePayload
{
    public int Number { get; set; }
    public string Operation { get; set; } = string.Empty;
}

/// <summary>
/// Результат вычислений.
/// </summary>
/// <remarks>
/// Computation result.
/// </remarks>
[DestinationNode(TestNodeNames.StorageNode)]
public sealed class ComputeResultPayload
{
    public int OriginalNumber { get; set; }
    public int Result { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Payload для тестирования хранения данных.
/// </summary>
/// <remarks>
/// Payload for data storage testing.
/// </remarks>
[DestinationNode(TestNodeNames.StorageNode)]
public sealed class StoreDataPayload
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Payload для простого тестового сообщения.
/// </summary>
/// <remarks>
/// Payload for simple test message.
/// </remarks>
public sealed class SimpleTestPayload
{
    public string Text { get; set; } = string.Empty;
    public int Counter { get; set; }
}
