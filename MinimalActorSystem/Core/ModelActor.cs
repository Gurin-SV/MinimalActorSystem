namespace MinimalActorSystem;

public abstract class ModelActor : SystemActor
{
    private readonly List<Actor> _pending = [];

    protected ModelActor(Guid uid, string name, IActorSystem system)
        : base(uid, name, system)
    {
        System.Send(new InitializeModelLetter(System.Uids.System, uid));
    }

    protected sealed override Task OnLetter(Letter letter)
    {
        if (letter is InitializeModelLetter)
        {
            BuildModel();
            return Task.CompletedTask;
        }
        return OnModelLetter(letter);
    }

    protected abstract void BuildModel();
    protected abstract Task OnModelLetter(Letter letter);

    protected Guid Create(Actor actor)
    {
        System.RegisterActor(actor);
        _pending.Add(actor);
        return actor.Uid;
    }

    protected void ReleaseAll()
    {
        _pending.Clear();
    }
}
