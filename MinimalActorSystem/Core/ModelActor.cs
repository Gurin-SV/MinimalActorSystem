namespace MinimalActorSystem;

public abstract class ModelActor : SystemActor
{
    private readonly List<Actor> _pending = [];

    protected ModelActor(Guid uid, string name, IActorSystem system)
        : base(uid, name, system)
    {
        Trace("Sending InitializeModelLetter to self");
        System.Send(new InitializeModelLetter(System.Uids.System, uid));
    }

    protected sealed override ValueTask OnLetter(Letter letter)
    {
        if (letter is InitializeModelLetter)
        {
            Trace("BuildModel starting");
            BuildModel();
            Trace($"BuildModel completed, {_pending.Count} actors pending");
            return default;
        }
        return OnModelLetter(letter);
    }

    protected abstract void BuildModel();
    protected abstract ValueTask OnModelLetter(Letter letter);

    protected Guid Create(Actor actor)
    {
        Trace($"Creating: {actor.Name}");
        System.RegisterActor(actor);
        _pending.Add(actor);
        return actor.Uid;
    }

    protected void ReleaseAll()
    {
        Trace($"ReleaseAll: {_pending.Count} actors");
        _pending.Clear();
    }
}
