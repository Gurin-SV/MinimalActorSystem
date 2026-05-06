using System.Collections.Concurrent;

namespace MinimalActorSystem;

public sealed class SystemTimeService : ITimeService
{
    private readonly ConcurrentQueue<PendingOp> _pending = [];
    private readonly Dictionary<CallbackKey, TimerEntry> _timers = [];
    private readonly SortedSet<ExpiryEntry> _expiryIndex = [];
    private readonly IActorSystem _system;
    private readonly Guid _uid;

    public DateTime UtcNow => DateTime.UtcNow;

    public SystemTimeService(IActorSystem system)
    {
        _system = system;
        _uid = system.Uids.TimeService;
        _system.Trace("SystemTimeService created");
        _ = RunAsync(_system.CancellationToken);
    }

    public void Register(DateTime deadline, TimeoutCallback callback)
    {
        _system.Trace($"Register timeout: actor={_system.GetActorName(callback.ActorUid)} callbackId={callback.CallbackId} deadline={deadline:HH:mm:ss.fff}");
        _pending.Enqueue(new RegisterOp(deadline, callback));
    }

    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        Register(UtcNow + timeout, callback);
    }

    public void Unregister(TimeoutCallback callback)
    {
        _system.Trace($"Unregister timeout: actor={_system.GetActorName(callback.ActorUid)} callbackId={callback.CallbackId}");
        _pending.Enqueue(new UnregisterOp(callback));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _system.Trace("loop started");
        while (!ct.IsCancellationRequested)
        {
            ApplyPendingOps();

            var now = UtcNow;
            while (_expiryIndex.Count > 0 && _expiryIndex.Min.Deadline <= now)
            {
                var entry = _expiryIndex.Min;
                _expiryIndex.Remove(entry);

                if (_timers.TryGetValue(entry.Key, out var timer) && timer.Deadline <= now)
                {
                    _timers.Remove(entry.Key);
                    _system.Trace($"Timeout fired: actor={_system.GetActorName(timer.Callback.ActorUid)} callbackId={timer.Callback.CallbackId}");
                    var letter = new TimeServiceLetter(_uid, timer.Callback.ActorUid, timer.Callback);
                    _system.Send(letter);
                }
            }

            try
            {
                await Task.Delay(1, ct);
            }
            catch (OperationCanceledException)
            {
                _system.Trace("loop cancelled");
                return;
            }
        }
        _system.Trace("loop finished");
    }

    private void ApplyPendingOps()
    {
        while (_pending.TryDequeue(out var op))
        {
            switch (op)
            {
                case RegisterOp reg:
                    {
                        var key = new CallbackKey(reg.Callback.ActorUid, reg.Callback.CallbackId);

                        if (_timers.TryGetValue(key, out var old))
                        {
                            _system.Trace($"Replacing timer: actor={_system.GetActorName(reg.Callback.ActorUid)} callbackId={reg.Callback.CallbackId}");
                            _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));
                        }

                        _timers[key] = new TimerEntry(reg.Deadline, reg.Callback);
                        _expiryIndex.Add(new ExpiryEntry(reg.Deadline, key));
                        break;
                    }
                case UnregisterOp unreg:
                    {
                        var key = new CallbackKey(unreg.Callback.ActorUid, unreg.Callback.CallbackId);

                        if (_timers.TryGetValue(key, out var old))
                        {
                            _timers.Remove(key);
                            _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));
                        }
                        break;
                    }
            }
        }
    }

    private sealed record CallbackKey(Guid ActorUid, int CallbackId);
    private sealed record TimerEntry(DateTime Deadline, TimeoutCallback Callback);
    private sealed record ExpiryEntry(DateTime Deadline, CallbackKey Key) : IComparable<ExpiryEntry>
    {
        public int CompareTo(ExpiryEntry? other)
        {
            if (other is null)
                return 1;

            int cmp = Deadline.CompareTo(other.Deadline);
            if (cmp != 0)
                return cmp;

            cmp = Key.ActorUid.CompareTo(other.Key.ActorUid);
            if (cmp != 0)
                return cmp;

            return Key.CallbackId.CompareTo(other.Key.CallbackId);
        }
    }

    private abstract record PendingOp;
    private sealed record RegisterOp(DateTime Deadline, TimeoutCallback Callback) : PendingOp;
    private sealed record UnregisterOp(TimeoutCallback Callback) : PendingOp;
}
