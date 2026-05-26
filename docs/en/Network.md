# MinimalActorSystem.Network - Distributed Actor System Design

## 1. Goals and Principles

Extension of `MinimalActorSystem` to support network communication without modifying the base library. Local actor systems run on network nodes and communicate with each other while minimizing network traffic.

**Key Principles:**
- Network logic is completely isolated in a separate `MinimalActorSystem.Network` assembly
- Network communication follows the "fire-and-forget" principle
- Timeouts and response waiting are implemented at the application actor level via `ITimeService`
- Specific transport implementations (HTTP, gRPC, WebSockets) remain the responsibility of the application developer
- Serialization uses JSON, extracted into a separate component

---

## 2. System Architecture

### 2.1 Components

| Component | Description |
|-----------|-------------|
| **Node** | Local actor system + network actor + node web service (optional) |
| **NetworkActor** | Single instance per node, `Uid = SystemUids.Network`. Dispatcher for incoming/outgoing network messages. Has two queues: local letters and incoming network messages. |
| **Router** | Name resolution web service. Maintains `node name → network address` table, periodically checks node availability via `/health`. Routers do not communicate with each other. |
| **Topology** | Common assembly for all nodes. Contains DTOs (Payload), routing attributes, lists of node names and routers. |

### 2.2 Data Flow

**Sending:**

    Local actor → IPayloadLetter (Receiver = SystemUids.Network) → NetworkActor →
    → node selection via [DestinationNode] attributes → NetworkLetter → IMessageSerializer → INetworkTransport

**Receiving:**

    INetworkTransport → IMessageSerializer → NetworkLetter → NetworkActor →
    → local actor selection via attributes/registration → create original letter (Sender = SystemUids.Network) →
    → System.Send() → target local actor

---

## 3. MinimalActorSystem.Network Assembly

### 3.1 NetworkActor (abstract base class)

    public abstract class NetworkActor : Actor
    {
        protected NetworkActor(IActorSystem system, IEnumerable<string> routerAddresses, int queueCapacity = 512);
        
        public async Task InitializeAsync(
            string nodeName,
            string nodeAddress,
            INetworkTransport transport,
            IRouterClient routerClient,
            Assembly topologyAssembly);
        
        // Routing dictionaries
        protected readonly Dictionary<Type, string[]> OutboundRoutes;
        protected readonly Dictionary<Type, Guid> InboundRoutes;
        
        // Register outbound route for Payload type
        protected void RegisterOutboundRoute<TPayload>(params string[] destinationNodeNames);
        protected void RegisterOutboundRoute(Type payloadType, params string[] destinationNodeNames);
        
        // Register inbound route for Payload type
        protected void RegisterInboundRoute<TPayload>(Guid localActorUid);
        
        // Set node selection strategy
        public void SetNodeSelectionStrategy(INodeSelectionStrategy strategy);
        
        // Cancel pending letter by local identifier
        public bool CancelPendingLetter(Guid localMessageId);
        
        // Node registry
        protected NodeRegistry NodeRegistry { get; }
    }

**Features:**
- Has two queues (letters from local actors + incoming network messages)
- Uses `[DestinationNode]` attributes on Payload type for outbound routing (auto-loaded via TopologyLoader)
- Uses `RegisterInboundRoute()` or `[HandlesPayload]` attributes for inbound routing
- Supports pending message queue (`IPendingMessageQueue`)
- Automatically loads routes from attributes during initialization

### 3.2 NodeRegistry (node registry)

    public enum NodeStatus { Inactive, Active }
    
    public sealed class NodeInfo
    {
        public string NodeName { get; }
        public string NodeAddress { get; set; }
        public NodeStatus Status { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime RegisteredAt { get; }
    }
    
    public sealed class NodeRegistry
    {
        public int Count { get; }
        public bool RegisterNode(string nodeName, string nodeAddress);
        public bool UnregisterNode(string nodeName);
        public bool TryGetNodeAddress(string nodeName, out string? nodeAddress);
        public NodeInfo? GetNodeInfo(string nodeName);
        public bool SetNodeStatus(string nodeName, NodeStatus status);
        public IReadOnlyList<NodeInfo> GetAllNodes();
        public IReadOnlyList<NodeInfo> GetActiveNodes();
        public IReadOnlyList<NodeInfo> GetInactiveNodes();
        public bool Contains(string nodeName);
        public void Clear();
    }

### 3.3 IPendingMessageQueue (pending message queue)

    public sealed class PendingMessage
    {
        public Guid LocalMessageId { get; }
        public object Payload { get; set; }
        public Type PayloadType { get; set; }
        public Type LetterType { get; set; }
        public string DestinationNode { get; set; }
        public string SourceNode { get; set; }
        public DateTime CreatedAt { get; }
        public DateTime? SentAt { get; set; }
        public bool IsSent { get; set; }
        public bool IsQueued { get; set; }
        public int AttemptCount { get; set; }
    }
    
    public interface IPendingMessageQueue
    {
        void Enqueue(PendingMessage message);
        IReadOnlyList<PendingMessage> DequeueForNode(string nodeName);
        bool Remove(Guid localMessageId);
        bool Contains(Guid localMessageId);
        PendingMessage? Get(Guid localMessageId);
        int Count { get; }
        int GetQueuedCountForNode(string nodeName);
        void Clear();
    }

### 3.4 INodeSelectionStrategy (node selection strategy)

    public interface INodeSelectionStrategy
    {
        string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName);
    }
    
    // Built-in strategies:
    public sealed class FirstAvailableStrategy : INodeSelectionStrategy;
    public sealed class LeastLoadedStrategy : INodeSelectionStrategy;
    public sealed class RoundRobinStrategy : INodeSelectionStrategy;

### 3.5 IMessageSerializer (message serializer)

    public interface IMessageSerializer
    {
        string Serialize(NetworkLetter letter);
        NetworkLetter? Deserialize(string data);
    }
    
    public sealed class JsonMessageSerializer : IMessageSerializer;

**Serialization format:** `"TypeName\0{...json...}"`, where `TypeName` is the AssemblyQualifiedName of the `NetworkLetter` type.

### 3.6 TopologyLoader (topology loader)

    public sealed class TopologyLoader
    {
        public TopologyLoader(Assembly assembly);
        public Assembly Assembly { get; }
        public IReadOnlyList<string> RouterAddresses { get; }
        public IReadOnlyList<string> NodeNames { get; }
        public IReadOnlyDictionary<Type, string[]> DestinationNodeRoutes { get; }
        public string[]? GetDestinationNodesForPayload(Type payloadType);
        public IReadOnlyList<Type> GetPayloadTypes();
    }

### 3.7 INetworkTransport (interface)

    public interface INetworkTransport : IDisposable
    {
        Task RegisterNodeAsync(string nodeName, CancellationToken cancellationToken = default);
        void SetMessageHandler(Func<string, string, Task> onMessageReceived);
        Task SendAsync(string nodeName, string serializedMessage, CancellationToken cancellationToken = default);
    }

### 3.8 IRouterClient (interface)

    public interface IRouterClient : IDisposable
    {
        Task RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default);
        Task<string?> ResolveNodeAddressAsync(string nodeName, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, string>> GetAllNodesAsync(CancellationToken cancellationToken = default);
        void SetNodeDiscoveredHandler(Func<string, string, Task> onNodeDiscovered);
        Task StartPollingAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default);
    }

### 3.9 IPayloadLetter (interface)

    public interface IPayloadLetter
    {
        object Payload { get; set; }
        Type PayloadType { get; }
    }

### 3.10 NetworkLetter (serializable container)

    public sealed class NetworkLetter
    {
        public string DestinationNodeName { get; set; }
        public string SourceNodeName { get; set; }
        public string PayloadJson { get; set; }
        public string PayloadTypeName { get; set; }
        public string LetterTypeName { get; set; }
        public Guid LocalLetterId { get; set; }
        
        [JsonIgnore]
        public Type PayloadType { get; }
        
        [JsonIgnore]
        public Type LetterType { get; }
        
        public T GetPayload<T>();
        public static NetworkLetter Create(
            string destinationNodeName,
            string sourceNodeName,
            object payload,
            Type payloadType,
            Type letterType,
            Guid localLetterId);
    }

### 3.11 Routing Attributes

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DestinationNodeAttribute : Attribute
    {
        public string NodeName { get; }
        public DestinationNodeAttribute(string nodeName);
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class HandlesPayloadAttribute : Attribute
    {
        public Type PayloadType { get; }
        public HandlesPayloadAttribute(Type payloadType);
    }

---

## 4. Topology Assembly (common for all nodes)

Contains:
- DTO classes (Payload) marked with `[DestinationNode]` attributes (multiple allowed)
- Letter classes implementing `IPayloadLetter`
- Constants: node names, router addresses

### 4.1 Example

    // Payload (DTO)
    [DestinationNode("ComputeNode")]
    [DestinationNode("BackupComputeNode")]
    public class PrimeCalculationPayload
    {
        public int Number { get; set; }
    }
    
    // Carrier letter
    public sealed class PrimeCalculationLetter : Letter, IPayloadLetter
    {
        public PrimeCalculationLetter(Guid sender, Guid receiver) : base(sender, receiver) { }
        public object Payload { get; set; }
        public Type PayloadType => typeof(PrimeCalculationPayload);
    }

---

## 5. Routing

### 5.1 Sending (local → remote)

- `NetworkActor` automatically loads routes from `[DestinationNode]` attributes via `TopologyLoader`
- If multiple nodes are specified, selects one using a strategy (default: `FirstAvailableStrategy`)
- Strategy can be changed via `SetNodeSelectionStrategy()`
- If node is not in registry — letter is placed in pending queue

### 5.2 Receiving (remote → local)

**Option A (declarative, via attributes):**

    public class ComputeNodeNetworkActor : NetworkActor
    {
        public ComputeNodeNetworkActor(IActorSystem system, IEnumerable<string> routerAddresses)
            : base(system, routerAddresses) { }
    
        [HandlesPayload(typeof(PrimeCalculationPayload))]
        public Guid PrimeCalculatorHandler => ComputeNodeUids.PrimeCalculator;
    }

**Option B (imperative, via code after initialization):**

    var actor = new MyNetworkActor(system, routerAddresses);
    await actor.InitializeAsync(nodeName, nodeAddress, transport, routerClient, topologyAssembly);
    
    // Register routes
    actor.RegisterOutboundRoute<PrimeCalculationPayload>("ComputeNode");
    actor.RegisterInboundRoute<PrimeCalculationPayload>(ComputeNodeUids.PrimeCalculator);

---

## 6. Fault Tolerance and Timeouts

### 6.1 Pending Messages

- If destination node is not in registry, `NetworkActor` stores `PendingMessage` in **pending queue** (`IPendingMessageQueue`)
- When router notifies of a new node — sends all accumulated letters for that node

### 6.2 Timeouts and Cancellation

- Actor expecting a response registers a timeout via `ITimeService`
- Each sent letter is assigned a local `Guid` (not transmitted over network)
- On timeout, actor sends a special letter with this `Guid` to `NetworkActor`
- `NetworkActor` finds the letter in the pending queue and removes it via `CancelPendingLetter`

---

## 7. Node Startup and Registration

1. Node starts, creates `ActorSystem` and `NetworkActor`
2. Calls `InitializeAsync` with node name, address, transport, router client, and topology assembly
3. Inside `InitializeAsync`:
   - Registers node with transport via `_transport.RegisterNodeAsync(nodeName)`
   - Sets message handler via `_transport.SetMessageHandler(OnNetworkMessageReceived)`
   - Registers node with all routers via `_routerClient.RegisterNodeAsync(nodeName, nodeAddress)`
   - Loads all known nodes via `_routerClient.GetAllNodesAsync()` into `NodeRegistry`
   - Sets node discovery handler via `_routerClient.SetNodeDiscoveredHandler(OnNodeDiscovered)`
   - Starts router polling via `_routerClient.StartPollingAsync(TimeSpan.FromSeconds(30))`
   - Automatically loads outbound routes from attributes via `TopologyLoader`
   - Automatically loads inbound routes from `[HandlesPayload]` attributes
4. Routers start periodically polling the node via `/health` (implementation-specific)
5. If node wants to send a message but destination node is not in registry — letter goes to pending queue
6. Upon successful resolution from router (new node discovered) — accumulated letters are sent

---

## 8. Router Interaction

- Routers **do not exchange data** with each other
- Each router independently maintains its own name → address table
- Node may receive multiple address variants for the same name (if routers diverge)
- Address selection is the responsibility of the transport

---

## 9. Application Developer Responsibilities

| Component | Implemented by Developer |
|-----------|--------------------------|
| Transport (`INetworkTransport`) | Fully, according to their standards |
| Router Client (`IRouterClient`) | Fully (HTTP/gRPC to their routers) |
| Node and Router Web Services | Fully |
| `Topology` Assembly | Fully |
| Concrete `NetworkActor` | Inherits from `NetworkActor`, adds handlers via `[HandlesPayload]` attributes |
| Security, Authentication | At transport and router level |

---

## 10. Non-Breaking Changes to MinimalActorSystem

A constant is added to `SystemUids`:

    public static readonly Guid Network = new("00000000-0000-0000-0000-000000000003");

No other changes are made to the base library.

---

## 11. Assembly Dependency Diagram

    MinimalActorSystem (core)
            ↑
    MinimalActorSystem.Network (network abstractions)
            ↑
    Topology (Payload, attributes, constants)
            ↑
    NodeApplication (concrete NetworkActor, transport, router client)

---

## 12. Testing

All components are covered by unit tests. The following test helpers are available:
- `FakeNetworkTransport` — fake transport implementation
- `FakeRouterClient` — fake router client implementation
- `TestNetworkActor` — test implementation of network actor
- `TestReceiverActor` — test actor for receiving messages
- `TestTopology` — test topology with sample DTOs and letters

**Number of tests:** 89

**Status:** All tests pass
