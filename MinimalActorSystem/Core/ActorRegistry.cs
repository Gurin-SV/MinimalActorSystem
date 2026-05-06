using System.Collections.Concurrent;

namespace MinimalActorSystem;

internal sealed class ActorRegistry(IActorSystem actorSystem)
{
    private readonly IActorSystem _actorSystem = actorSystem;
    private readonly ConcurrentDictionary<Guid, Actor> _actors = [];
    private TaskCompletionSource<bool>? _emptyTcs;

    public int Count => _actors.Count;

    public void Add(Actor actor)
    {
        _actors[actor.Uid] = actor;
    }

    public void Remove(Guid uid)
    {
        _actors.TryRemove(uid, out _);
        if (_actors.Count == 0)
        {
            _actorSystem.Trace("Registry empty");
            _emptyTcs?.TrySetResult(true);
        }
    }

    public bool TryGet(Guid uid, out Actor actor)
    {
        return _actors.TryGetValue(uid, out actor!);
    }

    public string GetName(Guid uid)
    {
        if (_actors.TryGetValue(uid, out var actor))
            return actor.Name;
        if (uid == _actorSystem.Uids.System)
            return "System";
        if (uid == _actorSystem.Uids.TimeService)
            return "TimeService";
        if (uid == _actorSystem.Uids.Model)
            return "Model";
        return uid.ToString();
    }

    public List<Actor> GetAll()
    {
        return [.. _actors.Values];
    }

    public Task WaitForEmptyAsync()
    {
        if (_actors.Count == 0)
            return Task.CompletedTask;
        _emptyTcs = new TaskCompletionSource<bool>();
        return _emptyTcs.Task;
    }
}
