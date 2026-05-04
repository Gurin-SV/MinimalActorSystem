namespace MinimalActorSystem;

public abstract class SystemActor : Actor
{
    protected SystemActor(Guid uid, string name, IActorSystem system)
        : base(uid, name, system)
    {
        system.RegisterActor(this);
    }
}
