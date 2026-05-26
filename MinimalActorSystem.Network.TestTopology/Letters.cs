namespace MinimalActorSystem.Network.TestTopology;

/// <summary>
/// Письмо для отправки ComputePayload.
/// </summary>
/// <remarks>
/// Letter for sending ComputePayload.
/// </remarks>
public sealed class ComputeLetter : Letter, IPayloadLetter
{
    public ComputeLetter(Guid sender, Guid receiver) : base(sender, receiver) { }

    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(ComputePayload);
}

/// <summary>
/// Письмо для отправки ComputeResultPayload.
/// </summary>
/// <remarks>
/// Letter for sending ComputeResultPayload.
/// </remarks>
public sealed class ComputeResultLetter : Letter, IPayloadLetter
{
    public ComputeResultLetter(Guid sender, Guid receiver) : base(sender, receiver) { }

    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(ComputeResultPayload);
}

/// <summary>
/// Письмо для отправки StoreDataPayload.
/// </summary>
/// <remarks>
/// Letter for sending StoreDataPayload.
/// </remarks>
public sealed class StoreDataLetter : Letter, IPayloadLetter
{
    public StoreDataLetter(Guid sender, Guid receiver) : base(sender, receiver) { }

    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(StoreDataPayload);
}

/// <summary>
/// Письмо для отправки SimpleTestPayload.
/// </summary>
/// <remarks>
/// Letter for sending SimpleTestPayload.
/// </remarks>
public sealed class SimpleTestLetter : Letter, IPayloadLetter
{
    public SimpleTestLetter(Guid sender, Guid receiver) : base(sender, receiver) { }

    public object Payload { get; set; } = null!;

    public Type PayloadType => typeof(SimpleTestPayload);
}
