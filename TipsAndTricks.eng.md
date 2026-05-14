# Tips and Tricks for Using MinimalActorSystem

This document collects practical techniques and patterns that simplify application actor development. Each tip is independent — you can read selectively, based on your task.

## 1. Circuit Breaker for External Calls

If your actor accesses an external HTTP service, database, message queue, or file system — sooner or later this resource will become unavailable. Without protection, the actor will repeatedly try in vain: wasting resources, clogging the queue, and generating errors in logs. Circuit Breaker solves this: after N consecutive failures, it disables attempts for a specified pause. No calls — no errors. After the pause, it allows one trial request — and either restores operation or goes back into pause.

### Three States

- **Closed** — normal operation. Requests pass through to the resource. Consecutive errors are counted. When the counter reaches the threshold — transition to Open.
- **Open** — requests are blocked. Instead of actual calls, rejection is returned immediately. After a specified timeout — transition to HalfOpen.
- **HalfOpen** — one trial request is allowed. Success — transition to Closed, error counter resets. Failure — back to Open.

### Implementation

Breaker is not a separate actor, but behavior inside your actor. You need: a state field, an error counter, a pause timeout, and a callback for it.

Create a timeout callback in the constructor:

    private readonly TimeoutCallback _openTimeoutCallback;
    private State _state = State.Closed;
    private int _failureCount;
    private const int Threshold = 5;
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);

    public ServiceCallerActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _openTimeoutCallback = new TimeoutCallback(Uid, callbackId: 1, OnOpenTimeout);
    }

    private void OnOpenTimeout()
    {
        _state = State.HalfOpen;
    }

Handle requests depending on state:

    private void HandleRequest(ServiceRequest req)
    {
        switch (_state)
        {
            case State.Open:
                RespondFailure(req);
                break;

            case State.Closed:
            case State.HalfOpen:
                SendToExternalService(req);
                break;
        }
    }

On success — reset:

    private void HandleSuccess()
    {
        _failureCount = 0;
        _state = State.Closed;
        System.TimeService.Unregister(_openTimeoutCallback);
    }

On failure — count and trip when reaching threshold:

    private void HandleFailure()
    {
        _failureCount++;

        if (_state == State.HalfOpen)
        {
            _state = State.Open;
            System.TimeService.Register(OpenTimeout, _openTimeoutCallback);
            return;
        }

        if (_state == State.Closed && _failureCount >= Threshold)
        {
            _state = State.Open;
            System.TimeService.Register(OpenTimeout, _openTimeoutCallback);
        }
    }

### Why This Works Without Locks

State, counter, and flags are ordinary instance fields of the actor. The actor processes messages strictly one at a time, so two threads never enter `HandleRequest` simultaneously. No `lock`, `Interlocked`, or `ConcurrentDictionary` is needed — the actor model itself guarantees sequential access.

### Testing

With virtual time, all transitions can be tested instantly. Set `Threshold` to 2, `OpenTimeout` to 10 seconds, send three requests in a row (two failures — circuit breaks), advance time by 10 seconds (transition to HalfOpen), send one more — and check whether it went to the service or immediately returned rejection.

---

## 2. Rendezvous with Timeout

Often an actor needs not just to send a message and forget, but to wait for a response from another actor — and continue the algorithm only after receiving the response or timeout. For example: requested configuration from ConfigActor — waiting for response to start work; requested permission from CoordinatorActor — waiting to perform an operation; sent a request to an external gateway — waiting for result.

Rendezvous is a pattern of temporary waiting for a response with timeout. The initiator actor sends a message, registers a timeout, and enters waiting mode. When the response arrives — timeout is cancelled, algorithm continues. When timeout fires — the actor reacts to the absence of response.

### Waiting State

For rendezvous, the actor needs state — whom and what it's waiting for. The simplest solution: store the expected response type and request context.

    private Type? _expectedResponseType;
    private object? _pendingContext;

The actor is waiting while `_expectedResponseType` is not null. All other messages at this point are either ignored or placed in a deferred queue (if logic requires strict ordering). Let's first consider the ignore variant — it's simpler.

### Timeout Callback

Created in the constructor. On firing, calls the `OnRendezvousTimeout` method:

    private readonly TimeoutCallback _rendezvousTimeout;

    public WorkerActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _rendezvousTimeout = new TimeoutCallback(Uid, callbackId: 1, OnRendezvousTimeout);
    }

    private void OnRendezvousTimeout()
    {
        var context = _pendingContext;
        _expectedResponseType = null;
        _pendingContext = null;

        System.Logger.LogWarning("{Name}: rendezvous with {Type} timed out", Name, _expectedResponseType?.Name);
        HandleTimeout(context);
    }

The `HandleTimeout` method is the reaction to absence of response. This could be retrying the request, returning an error to sender, entering emergency mode, or simply logging.

### Sending Request and Entering Rendezvous

    private static readonly TimeSpan DefaultRendezvousTimeout = TimeSpan.FromSeconds(30);

    public void RequestConfig(Guid configActorUid)
    {
        if (_expectedResponseType != null)
        {
            System.Logger.LogWarning("{Name}: already in rendezvous, request rejected", Name);
            return;
        }

        _expectedResponseType = typeof(ConfigResponse);
        _pendingContext = null;  // or some context object

        System.TimeService.Register(DefaultRendezvousTimeout, _rendezvousTimeout);
        System.Send(new ConfigRequest(Uid, configActorUid));
    }

### Handling Response

In `OnLetter`, add a check — if the actor is in rendezvous, it handles only the expected type:

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case ConfigResponse resp when _expectedResponseType == typeof(ConfigResponse):
                // Response received — cancel timeout, exit rendezvous
                System.TimeService.Unregister(_rendezvousTimeout);
                _expectedResponseType = null;
                _pendingContext = null;

                // Continue algorithm
                OnConfigReceived(resp);
                break;

            case TimeServiceLetter timeout:
                timeout.Callback.Action();
                break;

            default:
                // In waiting mode, ignore other messages
                if (_expectedResponseType != null)
                {
                    System.Logger.LogDebug("{Name}: in rendezvous, ignoring {Type}", Name, letter.GetType().Name);
                    break;
                }
                HandleOtherLetter(letter);
                break;
        }
        return default;
    }

### Retry with Exponential Backoff

Rendezvous timeout can be combined with request retry. After timeout fires, the actor increments attempt counter and sends the request again — with increased pause.

    private int _retryCount;
    private static readonly TimeSpan[] RetryBackoff = 
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    };

    private void OnRendezvousTimeout()
    {
        if (_retryCount < RetryBackoff.Length)
        {
            var target = _pendingTarget;  // stored whom to send to
            System.TimeService.Register(RetryBackoff[_retryCount], _rendezvousTimeout);
            _retryCount++;
            System.Send(new ConfigRequest(Uid, target));
        }
        else
        {
            _expectedResponseType = null;
            _pendingContext = null;
            _retryCount = 0;
            HandleDefinitiveTimeout();
        }
    }

On receiving response, attempt counter resets.

### Testing

With virtual time, rendezvous is easy to test. Scenario "response arrived on time": send request, advance time by 5 seconds (less than timeout), send response from second actor via `System.Send` — verify timeout cancelled and algorithm continued. Scenario "timeout": send request, advance time beyond timeout, verify `HandleTimeout` called. Scenario "retry": advance time until timeout fires, verify request sent again, then deliver response — verify counter reset.

### When to Use

- Requesting configuration, permission, quota from another actor
- Waiting for result from gateway actor to external service
- Coordination between two actors where one makes request and waits for confirmation
- Any "request-response" interaction within the system

Rendezvous is local waiting inside one actor. It doesn't block threads, doesn't pause other actors, and doesn't require system primitives like `TaskCompletionSource`. The actor simply waits for either the needed message or timeout — whichever comes first.

---

## 3. Accumulator — Mutable Movable Message

Sometimes a message must pass through a chain of actors, and each adds its part of data. Instead of each actor creating a new message and copying data, you can use one message instance that passes from actor to actor, mutating along the way.

System contracts explicitly allow this: `Sender` and `Receiver` fields are mutable, and the letter can be reused. Each actor in the chain swaps `Sender`/`Receiver` or substitutes new identifiers, adds its data to letter fields, and sends further.

### Example: Collecting Signatures Under a Document

Accumulator letter:

    public sealed class ApprovalChain : Letter
    {
        public List<ApprovalStep> Steps { get; } = new();
        public bool IsApproved => Steps.All(s => s.Approved);

        public ApprovalChain(Guid sender, Guid receiver) : base(sender, receiver) { }
    }

    public sealed class ApprovalStep
    {
        public Guid ApproverUid { get; init; }
        public string ApproverName { get; init; }
        public bool Approved { get; set; }
        public string? Comment { get; set; }
    }

First actor creates the letter and starts the chain:

    var chain = new ApprovalChain(Uid, firstApproverUid);
    chain.Steps.Add(new ApprovalStep { ApproverUid = firstApproverUid, ApproverName = "Manager" });
    chain.Steps.Add(new ApprovalStep { ApproverUid = secondApproverUid, ApproverName = "Director" });
    System.Send(chain);

Approver actor receives the letter, finds its step, makes a decision, and sends further:

    protected override ValueTask OnLetter(Letter letter)
    {
        if (letter is ApprovalChain chain)
        {
            var myStep = chain.Steps.First(s => s.ApproverUid == Uid);
            myStep.Approved = true;
            myStep.Comment = "Approved";

            // Send to next or return to initiator
            var nextStep = chain.Steps.FirstOrDefault(s => !s.Approved);
            if (nextStep != null)
            {
                chain.Sender = Uid;
                chain.Receiver = nextStep.ApproverUid;
            }
            else
            {
                chain.Sender = Uid;
                chain.Receiver = chain.Steps[0].ApproverUid;  // back to initiator
            }
            System.Send(chain);
        }
        return default;
    }

No copying, no allocations on each step. The letter accumulates state and moves through the chain.

### When Useful

- Processing chains (each adds its stage)
- Collecting results of parallel requests (one coordinator actor)
- Forming composite response from multiple sources

### Caution

Accumulator breaks message immutability. If the letter accidentally goes to two places simultaneously — race condition occurs. Use only for strictly sequential chains or when the letter owner is the sole writer.

---

## 4. Delegate — Stateless Actor for a Task

Sometimes you need to react to one specific event or perform one action — and creating a full `Actor` subclass seems excessive. The "Delegate" pattern is an actor that receives a lambda or delegate in constructor and calls it upon receiving a message.

### Simple Delegate

    public sealed class DelegateActor : Actor
    {
        private readonly Func<Letter, ValueTask> _handler;

        public DelegateActor(IActorSystem system, Guid uid, string name,
            Func<Letter, ValueTask> handler)
            : base(system, uid, name)
        {
            _handler = handler;
        }

        protected override ValueTask OnLetter(Letter letter) => _handler(letter);
    }

Usage:

    var actor = new DelegateActor(system, Guid.NewGuid(), "one-shot",
        async letter =>
        {
            if (letter is SomeResponse resp)
            {
                // handle response
            }
        });

### Delegate with Timer

You can create an actor that simply waits specified time and performs an action:

    public sealed class DelayedActionActor : Actor
    {
        private readonly Action _action;
        private readonly TimeoutCallback _timer;

        public DelayedActionActor(IActorSystem system, Guid uid, string name,
            TimeSpan delay, Action action)
            : base(system, uid, name)
        {
            _action = action;
            _timer = new TimeoutCallback(Uid, callbackId: 1, OnTimer);
            system.TimeService.Register(delay, _timer);
        }

        private void OnTimer()
        {
            _action();
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeout)
                timeout.Callback.Action();
            return default;
        }
    }

After performing the action, the actor removes itself from registry.

### When Useful

- One-time reactions (requested — responded — forgot)
- Adapters between different letter interfaces
- Test stubs that don't need a full class

---

## 5. Router — Message by Criterion

When there are several similar actors in the system, the sender doesn't always know exactly whom to address the message to. A router is an actor that receives a message, determines the target recipient by its content, and forwards it.

### Example: Round-robin Load Balancer

    public sealed class RoundRobinRouter : Actor
    {
        private readonly List<Guid> _workers;
        private int _index;

        public RoundRobinRouter(IActorSystem system, Guid uid, string name,
            IEnumerable<Guid> workers)
            : base(system, uid, name)
        {
            _workers = workers.ToList();
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (_workers.Count == 0)
                return default;

            var target = _workers[_index];
            _index = (_index + 1) % _workers.Count;

            letter.Sender = Uid;
            letter.Receiver = target;
            System.Send(letter);
            return default;
        }
    }

### Example: Key-based Routing

    public sealed class ShardRouter : Actor
    {
        private readonly Dictionary<int, Guid> _shards;

        public ShardRouter(IActorSystem system, Guid uid, string name,
            Dictionary<int, Guid> shards)
            : base(system, uid, name)
        {
            _shards = shards;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is IShardable shardable)
            {
                var shardKey = shardable.ShardKey;
                if (_shards.TryGetValue(shardKey, out var target))
                {
                    letter.Sender = Uid;
                    letter.Receiver = target;
                    System.Send(letter);
                }
            }
            return default;
        }
    }

The `IShardable` interface is a marker on the letter that allows the router to extract the sharding key without knowing the specific type.

### When Useful

- Pool of similar handlers (load balancing)
- Data sharding by owner actors
- Conditional routing: critical messages to some actors, regular ones to others

---

## 6. Watchdog — Child Actor Health Monitoring

Parent creates a child actor and wants to know if it's alive. Watchdog periodically sends `Ping` to the child actor and waits for `Pong`. If no response arrives within allotted time — child is considered dead, and parent takes measures.

Unlike system supervisor, Watchdog is an application pattern built into parent logic. No privileged access to child actor is needed.

### Implementation

    private readonly TimeoutCallback _watchdogTimer;
    private Guid _childUid;
    private bool _pongReceived;

    public ParentActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _watchdogTimer = new TimeoutCallback(Uid, callbackId: 1, OnWatchdogTimeout);
    }

    private void StartWatching(Guid childUid)
    {
        _childUid = childUid;
        SendPing();
    }

    private void SendPing()
    {
        _pongReceived = false;
        System.TimeService.Register(TimeSpan.FromSeconds(5), _watchdogTimer);
        System.Send(new Ping(Uid, _childUid));
    }

    private void OnWatchdogTimeout()
    {
        if (!_pongReceived)
        {
            System.Logger.LogWarning("{Name}: child actor {Child} not responding", Name, GetChildName());
            HandleChildLost();
        }
    }

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case Pong pong when pong.Sender == _childUid:
                _pongReceived = true;
                System.TimeService.Unregister(_watchdogTimer);
                // After some time — check again
                ScheduleNextPing();
                break;

            case TimeServiceLetter timeout:
                timeout.Callback.Action();
                break;
        }
        return default;
    }

### When Useful

- Child actor works with unreliable resource and may get stuck
- Long background operations that need monitoring
- Soft degradation: lost handler — create new one

---

## 7. Deferred Queue — Messages for Later

An actor may receive a message that cannot be processed right now (not ready, no data, waiting for another message). Instead of ignoring or throwing an error, the actor puts the message in a deferred queue and returns to it later.

Unlike rendezvous, where actor waits for exactly one response, here the actor continues processing other messages, and reviews deferred ones by event or timer.

### Implementation with Double Queue

    private readonly Queue<Letter> _deferred = new();

    protected override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case DataReady ready:
                // Data ready — process deferred
                await ProcessDeferred();
                break;

            case Request req when !_dataReady:
                // Not ready yet — defer
                _deferred.Enqueue(req);
                break;

            case Request req:
                // Ready — process immediately
                HandleRequest(req);
                break;
        }
    }

    private async Task ProcessDeferred()
    {
        while (_deferred.Count > 0)
        {
            var letter = _deferred.Dequeue();
            if (letter is Request req)
                HandleRequest(req);
        }
    }

### When Useful

- Buffering requests until initialization completes
- Accumulating data for batch processing
- Reordering: arrived too early — wait until proper time

---

## 8. Stub for Tests

When testing an actor that depends on another actor, it's convenient to substitute a stub — an actor that accepts messages and either does nothing or responds in a predefined manner.

### Null Stub

Accepts any messages and stays silent:

    public sealed class NullActor : Actor
    {
        public NullActor(IActorSystem system, Guid uid) 
            : base(system, uid, "null") { }

        protected override ValueTask OnLetter(Letter letter) => default;
    }

### Stub with Response

On receiving a message of certain type, responds with predefined message:

    public sealed class StubActor : Actor
    {
        private readonly Type _expectedType;
        private readonly Letter _response;

        public StubActor(IActorSystem system, Guid uid, Type expectedType, Letter response)
            : base(system, uid, "stub")
        {
            _expectedType = expectedType;
            _response = response;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter.GetType() == _expectedType)
            {
                _response.Sender = Uid;
                _response.Receiver = letter.Sender;
                System.Send(_response);
            }
            return default;
        }
    }

Usage in test:

    var stub = new StubActor(system, Guid.NewGuid(),
        typeof(ConfigRequest),
        new ConfigResponse(Uid: Guid.Empty, receiver: Guid.Empty, configData: "..."));
    system.RegisterActor(stub);

    // Tested actor sends ConfigRequest to stub
    // and receives ConfigResponse with predefined data

### When Useful

- Isolating tested actor from real dependencies
- Emulating responses from external gateways
- Verifying that actor sends messages of correct type and with correct data

---

## 9. Performance and Its Implications

Benchmark results on 6000 actors and 30 million messages provide guidelines for designing application actors. Here are key takeaways.

### Numbers

- **2.3 million messages per second** — with thousands of active actors
- **0.43 microseconds per message** — including delivery and handling
- **52 milliseconds to create 6000 actors** — creation is practically free
- **36.5 MB memory for 6000 actors** — about 6 KB per actor with 256-message queue
- **30 threads at peak load** — thread pool scales automatically under load
- **0 lost messages** — `DropWrite` on queue overflow never triggered

### What This Means for Your Actors

**Actors are cheap.** Don't hesitate to create hundreds and thousands of actors. Every sensor, every connection, every user session — everything can be a separate actor. No architectural limit on quantity.

**Messages are faster than any external operations.** 0.43 μs per message — roughly 10,000 times faster than disk read and 100,000 times faster than network call. All coordination inside actor system is negligibly cheap compared to any I/O.

**Memory is not a problem.** 6 KB per actor — less than HTTP request size. Even on a modest server with 1 GB free memory, you can hold tens of thousands of actors.

**Threads scale automatically.** No need to configure pools or limits. Actors are not bound to threads — one thread serves many actors, switching between them via `await`.

### Where Memory Goes

Out of 2760 MB allocated memory, most is the messages themselves (30 million letters). Each letter is a heap object. If millions of messages circulate in your system, letter size matters. Use data structures inside letters economically, avoid nested collections and long strings.

### System Limitations

**Queue is limited.** By default 256 messages per actor. If actor processes slowly (e.g., waiting for external HTTP) and senders flood quickly — queue fills up, and new messages are silently dropped. For fast internal operations (like in benchmark — `Ping`/`Pong` without I/O) overflow doesn't occur. But if actor accesses database or external service — increase `QueueCapacity` on creation or put Breaker/Throttle before it.

**Processing is strictly sequential.** One actor — one message at a time. If actor performs long operation (e.g., waits 5 seconds for HTTP response), its queue will stall. Offload long waits to separate actor or use timeouts to free the queue.
