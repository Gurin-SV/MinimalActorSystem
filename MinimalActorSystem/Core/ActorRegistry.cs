using System.Collections.Concurrent;

namespace MinimalActorSystem;

/// <summary>
/// Внутренний реестр акторов. Единственное место в системе, где хранятся прямые ссылки на экземпляры <see cref="Actor"/>.
/// Потокобезопасен. Недоступен внешнему коду.
/// </summary>
/// <param name="actorSystem">Акторная система, которой принадлежит реестр.</param>
internal sealed class ActorRegistry(IActorSystem actorSystem)
{
    private readonly IActorSystem _actorSystem = actorSystem;
    private readonly ConcurrentDictionary<Guid, Actor> _actors = [];
    private readonly object _emptyLock = new();
    private TaskCompletionSource<bool>? _emptyTcs;

    /// <summary>
    /// Текущее количество зарегистрированных акторов.
    /// </summary>
    public int Count => _actors.Count;

    /// <summary>
    /// Добавляет актор в реестр.
    /// </summary>
    /// <param name="actor">Актор для регистрации.</param>
    public void Add(Actor actor)
    {
        _actors[actor.Uid] = actor;
    }

    /// <summary>
    /// Удаляет актор из реестра по идентификатору.
    /// Если реестр становится пустым, сигнализирует об этом через <see cref="WaitForEmptyAsync"/>.
    /// </summary>
    /// <param name="uid">Идентификатор удаляемого актора.</param>
    public void Remove(Guid uid)
    {
        _actors.TryRemove(uid, out _);

        if (_actors.Count == 0)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_emptyLock)
            {
                tcs = _emptyTcs;
                _emptyTcs = null;
            }
            tcs?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Пытается найти актор по идентификатору.
    /// </summary>
    /// <param name="uid">Идентификатор актора.</param>
    /// <param name="actor">Найденный актор или <c>null</c>.</param>
    /// <returns><c>true</c>, если актор найден; <c>false</c> в противном случае.</returns>
    public bool TryGet(Guid uid, out Actor actor)
    {
        return _actors.TryGetValue(uid, out actor!);
    }

    /// <summary>
    /// Возвращает имя актора по идентификатору.
    /// Для системных идентификаторов возвращает предопределённые имена.
    /// Если актор не найден — возвращает строковое представление <see cref="Guid"/>.
    /// </summary>
    /// <param name="uid">Идентификатор актора.</param>
    /// <returns>Имя актора.</returns>
    public string GetName(Guid uid)
    {
        if (_actors.TryGetValue(uid, out var actor))
            return actor.Name;
        if (uid == SystemUids.System)
            return "System";
        if (uid == SystemUids.TimeService)
            return "TimeService";
        return uid.ToString();
    }

    /// <summary>
    /// Возвращает снапшот всех зарегистрированных акторов.
    /// </summary>
    /// <returns>Список акторов на момент вызова.</returns>
    public List<Actor> GetAll()
    {
        return [.. _actors.Values];
    }

    /// <summary>
    /// Возвращает задачу, которая завершится, когда реестр станет пустым.
    /// Если реестр уже пуст на момент вызова, возвращает <see cref="Task.CompletedTask"/>.
    /// </summary>
    /// <returns>Задача, представляющая ожидание опустошения реестра.</returns>
    public Task WaitForEmptyAsync()
    {
        lock (_emptyLock)
        {
            if (_actors.Count == 0)
                return Task.CompletedTask;

            _emptyTcs = new TaskCompletionSource<bool>();
            return _emptyTcs.Task;
        }
    }
}
