namespace MinimalActorSystem;

public sealed class SystemUids
{
    public Guid System { get; } = new("00000000-0000-0000-0000-000000000001");
    public Guid TimeService { get; } = new("00000000-0000-0000-0000-000000000002");
    public Guid Model { get; } = new("00000000-0000-0000-0000-000000000003");
}
