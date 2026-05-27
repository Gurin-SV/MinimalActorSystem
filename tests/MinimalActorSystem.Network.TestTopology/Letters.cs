namespace MinimalActorSystem.Network.TestTopology;

/// <summary>
/// Письмо для отправки ComputePayload.
/// </summary>
/// <remarks>
/// Letter for sending ComputePayload.
/// </remarks>
public sealed class ComputeLetter(Guid sender, Guid receiver) : Letter(sender, receiver), IPayloadLetter
{
    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(ComputePayload);
}

/// <summary>
/// Письмо для отправки ComputeResultPayload.
/// </summary>
/// <remarks>
/// Letter for sending ComputeResultPayload.
/// </remarks>
public sealed class ComputeResultLetter(Guid sender, Guid receiver) : Letter(sender, receiver), IPayloadLetter
{
    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(ComputeResultPayload);
}

/// <summary>
/// Письмо для отправки StoreDataPayload.
/// </summary>
/// <remarks>
/// Letter for sending StoreDataPayload.
/// </remarks>
public sealed class StoreDataLetter(Guid sender, Guid receiver) : Letter(sender, receiver), IPayloadLetter
{
    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(StoreDataPayload);
}

/// <summary>
/// Письмо для отправки SimpleTestPayload.
/// </summary>
/// <remarks>
/// Letter for sending SimpleTestPayload.
/// </remarks>
public sealed class SimpleTestLetter(Guid sender, Guid receiver) : Letter(sender, receiver), IPayloadLetter
{
    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(SimpleTestPayload);
}
