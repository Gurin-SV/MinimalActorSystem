using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;

namespace MinimalActorSystem.Network;

/// <summary>
/// Абстрактный базовый класс сетевого актора.
/// Обеспечивает маршрутизацию писем между узлами сети.
/// </summary>
/// <remarks>
/// Abstract base class for network actor.
/// Provides routing of letters between network nodes.
/// </remarks>
public abstract class NetworkActor : Actor
{
    private readonly NodeRegistry _nodeRegistry = new();
    private readonly IPendingMessageQueue _pendingQueue;
    private readonly List<string> _routerAddresses;
    private INetworkTransport? _transport;
    private IRouterClient? _routerClient;
    private Assembly? _topologyAssembly;
    private bool _isInitialized;
    private bool _isShuttingDown;
    private INodeSelectionStrategy _nodeSelectionStrategy = new FirstAvailableStrategy();
    private readonly IMessageSerializer _serializer = new JsonMessageSerializer();

    /// <summary>
    /// Имя текущего узла.
    /// </summary>
    protected string ThisNodeName { get; private set; } = string.Empty;

    /// <summary>
    /// Сетевой адрес текущего узла.
    /// </summary>
    protected string ThisNodeAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Реестр узлов сети.
    /// </summary>
    protected NodeRegistry NodeRegistry => _nodeRegistry;

    /// <summary>
    /// Словарь исходящей маршрутизации: тип Payload -> список имён узлов назначения.
    /// </summary>
    protected readonly Dictionary<Type, string[]> OutboundRoutes = [];

    /// <summary>
    /// Словарь входящей маршрутизации: тип Payload -> идентификатор локального актора-обработчика.
    /// </summary>
    protected readonly Dictionary<Type, Guid> InboundRoutes = [];

    /// <summary>
    /// Создаёт новый экземпляр сетевого актора.
    /// </summary>
    /// <param name="system">Акторная система.</param>
    /// <param name="routerAddresses">Список адресов роутеров.</param>
    /// <param name="queueCapacity">Размер очереди сообщений.</param>
    protected NetworkActor(IActorSystem system, IEnumerable<string> routerAddresses, int queueCapacity = 512)
        : base(system, SystemUids.Network, "network", queueCapacity)
    {
        _routerAddresses = [.. routerAddresses];
        _pendingQueue = new PendingMessageQueue();
        System.Logger.LogDebug("NetworkActor created, routers: {RouterCount}", _routerAddresses.Count);
    }

    /// <summary>
    /// Инициализирует сетевого актора.
    /// </summary>
    /// <param name="nodeName">Имя текущего узла.</param>
    /// <param name="nodeAddress">Сетевой адрес текущего узла.</param>
    /// <param name="transport">Транспорт для сетевого взаимодействия.</param>
    /// <param name="routerClient">Клиент роутера для разрешения имён.</param>
    /// <param name="topologyAssembly">Сборка с типами Payload и Letter (Topology).</param>
    public async Task InitializeAsync(
        string nodeName,
        string nodeAddress,
        INetworkTransport transport,
        IRouterClient routerClient,
        Assembly topologyAssembly)
    {
        if (_isInitialized)
        {
            System.Logger.LogWarning("NetworkActor already initialized");
            return;
        }

        ThisNodeName = nodeName;
        ThisNodeAddress = nodeAddress;
        _transport = transport;
        _routerClient = routerClient;
        _topologyAssembly = topologyAssembly;

        await _transport.RegisterNodeAsync(nodeName);
        _transport.SetMessageHandler(OnNetworkMessageReceived);

        await _routerClient.RegisterNodeAsync(nodeName, nodeAddress);

        IReadOnlyDictionary<string, string> allNodes = await _routerClient.GetAllNodesAsync();
        foreach (KeyValuePair<string, string> node in allNodes)
        {
            _nodeRegistry.RegisterNode(node.Key, node.Value);
            _nodeRegistry.SetNodeStatus(node.Key, NodeStatus.Active);
        }

        _routerClient.SetNodeDiscoveredHandler(OnNodeDiscovered);
        await _routerClient.StartPollingAsync(TimeSpan.FromSeconds(30));

        // Автоматическая загрузка исходящих маршрутов из атрибутов Topology
        var topologyLoader = new TopologyLoader(topologyAssembly);
        foreach (KeyValuePair<Type, string[]> route in topologyLoader.DestinationNodeRoutes)
        {
            if (!OutboundRoutes.ContainsKey(route.Key))
            {
                RegisterOutboundRoute(route.Key, route.Value);
            }
        }

        // Автоматическая загрузка входящих маршрутов из атрибутов HandlesPayload
        RegisterInboundRoutesFromHandlers();

        _isInitialized = true;
        System.Logger.LogInformation("NetworkActor initialized: node {NodeName} at {NodeAddress}",
            nodeName, nodeAddress);
    }

    /// <summary>
    /// Устанавливает стратегию выбора узла.
    /// </summary>
    /// <param name="strategy">Стратегия выбора узла.</param>
    public void SetNodeSelectionStrategy(INodeSelectionStrategy strategy)
    {
        _nodeSelectionStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        System.Logger.LogDebug("Node selection strategy changed to {StrategyName}", strategy.GetType().Name);
    }

    /// <summary>
    /// Регистрирует исходящий маршрут для типа Payload (generic).
    /// </summary>
    /// <typeparam name="TPayload">Тип Payload.</typeparam>
    /// <param name="destinationNodeNames">Имена узлов назначения.</param>
    protected void RegisterOutboundRoute<TPayload>(params string[] destinationNodeNames)
    {
        OutboundRoutes[typeof(TPayload)] = destinationNodeNames;
        System.Logger.LogDebug("Registered outbound route for {PayloadType} -> {Nodes}",
            typeof(TPayload).Name, string.Join(", ", destinationNodeNames));
    }

    /// <summary>
    /// Регистрирует исходящий маршрут для типа Payload (по Type).
    /// </summary>
    /// <param name="payloadType">Тип Payload.</param>
    /// <param name="destinationNodeNames">Имена узлов назначения.</param>
    protected void RegisterOutboundRoute(Type payloadType, params string[] destinationNodeNames)
    {
        OutboundRoutes[payloadType] = destinationNodeNames;
        System.Logger.LogDebug("Registered outbound route for {PayloadType} -> {Nodes}",
            payloadType.Name, string.Join(", ", destinationNodeNames));
    }

    /// <summary>
    /// Регистрирует входящий маршрут для типа Payload.
    /// </summary>
    /// <typeparam name="TPayload">Тип Payload.</typeparam>
    /// <param name="localActorUid">Идентификатор локального актора-обработчика.</param>
    protected void RegisterInboundRoute<TPayload>(Guid localActorUid)
    {
        InboundRoutes[typeof(TPayload)] = localActorUid;
        System.Logger.LogDebug("Registered inbound route for {PayloadType} -> {ActorUid}",
            typeof(TPayload).Name, localActorUid);
    }

    /// <summary>
    /// Регистрирует входящие маршруты для методов и свойств, помеченных атрибутом HandlesPayloadAttribute.
    /// </summary>
    protected void RegisterInboundRoutesFromHandlers()
    {
        MethodInfo[] methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (MethodInfo? method in methods)
        {
            HandlesPayloadAttribute attribute = method.GetCustomAttribute<HandlesPayloadAttribute>();
            if (attribute == null)
                continue;

            if (method.ReturnType != typeof(Guid))
            {
                System.Logger.LogWarning("Method {MethodName} has HandlesPayloadAttribute but does not return Guid", method.Name);
                continue;
            }

            try
            {
                var actorUid = (Guid)method.Invoke(this, null)!;
                InboundRoutes[attribute.PayloadType] = actorUid;
                System.Logger.LogDebug("Registered inbound route for {PayloadType} -> {ActorUid}",
                    attribute.PayloadType.Name, actorUid);
            }
            catch (Exception ex)
            {
                System.Logger.LogError(ex, "Failed to invoke handler method {MethodName}", method.Name);
            }
        }

        PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (PropertyInfo? property in properties)
        {
            HandlesPayloadAttribute attribute = property.GetCustomAttribute<HandlesPayloadAttribute>();
            if (attribute == null)
                continue;

            if (property.PropertyType != typeof(Guid))
            {
                System.Logger.LogWarning("Property {PropertyName} has HandlesPayloadAttribute but does not return Guid", property.Name);
                continue;
            }

            try
            {
                var actorUid = (Guid)property.GetValue(this)!;
                InboundRoutes[attribute.PayloadType] = actorUid;
                System.Logger.LogDebug("Registered inbound route for {PayloadType} -> {ActorUid} from property",
                    attribute.PayloadType.Name, actorUid);
            }
            catch (Exception ex)
            {
                System.Logger.LogError(ex, "Failed to get value from property {PropertyName}", property.Name);
            }
        }
    }

    /// <summary>
    /// Обрабатывает входящие письма от локальных акторов.
    /// </summary>
    protected sealed override async ValueTask OnLetter(Letter letter)
    {
        if (_isShuttingDown)
        {
            System.Logger.LogWarning("NetworkActor is shutting down, letter dropped");
            return;
        }

        if (letter is IPayloadLetter payloadLetter)
        {
            await HandleOutgoingLetter(payloadLetter);
        }
        else
        {
            System.Logger.LogWarning("Received letter does not implement IPayloadLetter: {LetterType}",
                letter.GetType().Name);
        }
    }

    /// <summary>
    /// Обрабатывает исходящее письмо от локального актора.
    /// </summary>
    private async Task HandleOutgoingLetter(IPayloadLetter payloadLetter)
    {
        Type payloadType = payloadLetter.PayloadType;

        if (!OutboundRoutes.TryGetValue(payloadType, out var destinationNodes) || destinationNodes.Length == 0)
        {
            System.Logger.LogWarning("No outbound route for Payload type {PayloadType}", payloadType.Name);
            return;
        }

        string destinationNode;

        // Проверяем, может быть целевой узел - это мы сами
        if (destinationNodes.Length == 1 && destinationNodes[0] == ThisNodeName)
        {
            destinationNode = ThisNodeName;
        }
        else
        {
            var selectedNode = SelectDestinationNode(destinationNodes);
            if (string.IsNullOrEmpty(selectedNode))
            {
                System.Logger.LogWarning("Failed to select destination node for {PayloadType}", payloadType.Name);
                return;
            }
            destinationNode = selectedNode;
        }

        var localMessageId = Guid.NewGuid();
        var pendingMessage = new PendingMessage(localMessageId, destinationNode)
        {
            Payload = payloadLetter.Payload,
            PayloadType = payloadType,
            LetterType = payloadLetter.GetType(),
            SourceNode = ThisNodeName,
            CreatedAt = DateTime.UtcNow
        };

        _pendingQueue.Enqueue(pendingMessage);

        if (destinationNode == ThisNodeName)
        {
            await DeliverLocally(pendingMessage);
        }
        else
        {
            await SendOrQueueLetter(pendingMessage);
        }
    }

    /// <summary>
    /// Выбирает узел назначения из списка с использованием стратегии.
    /// </summary>
    private string? SelectDestinationNode(string[] nodes)
    {
        return _nodeSelectionStrategy.SelectNode(nodes, _nodeRegistry, ThisNodeName);
    }

    /// <summary>
    /// Отправляет письмо или помещает в очередь отложенных.
    /// </summary>
    private async Task SendOrQueueLetter(PendingMessage pendingMessage)
    {
        if (pendingMessage.DestinationNode == ThisNodeName)
        {
            await DeliverLocally(pendingMessage);
            return;
        }

        if (_nodeRegistry.Contains(pendingMessage.DestinationNode))
        {
            await SendLetter(pendingMessage);
        }
        else
        {
            pendingMessage.IsQueued = true;
            System.Logger.LogDebug("Letter {LetterId} queued for node {Node}",
                pendingMessage.LocalMessageId, pendingMessage.DestinationNode);
        }
    }

    /// <summary>
    /// Отправляет письмо на удалённый узел.
    /// </summary>
    private async Task SendLetter(PendingMessage pendingMessage)
    {
        if (_transport == null)
        {
            System.Logger.LogError("Transport is not initialized");
            return;
        }

        var networkLetter = NetworkLetter.Create(
            pendingMessage.DestinationNode,
            ThisNodeName,
            pendingMessage.Payload,
            pendingMessage.PayloadType,
            pendingMessage.LetterType,
            pendingMessage.LocalMessageId);

        var serialized = _serializer.Serialize(networkLetter);

        System.Logger.LogDebug("Sending letter {LetterId} to node {Node}",
            pendingMessage.LocalMessageId, pendingMessage.DestinationNode);

        await _transport.SendAsync(pendingMessage.DestinationNode, serialized);

        pendingMessage.IsSent = true;
        pendingMessage.SentAt = DateTime.UtcNow;
        _pendingQueue.Remove(pendingMessage.LocalMessageId);
    }

    /// <summary>
    /// Доставляет письмо локальному актору.
    /// </summary>
    private async Task DeliverLocally(PendingMessage pendingMessage)
    {
        Type payloadType = pendingMessage.PayloadType;

        if (!InboundRoutes.TryGetValue(payloadType, out Guid targetActorUid))
        {
            System.Logger.LogWarning("No inbound route for Payload type {PayloadType}", payloadType.Name);
            return;
        }

        var letter = (Letter)Activator.CreateInstance(
            pendingMessage.LetterType,
            SystemUids.Network,
            targetActorUid)!;

        ((IPayloadLetter)letter).Payload = pendingMessage.Payload;

        System.Logger.LogDebug("Local delivery of letter {LetterId} to actor {ActorUid}",
            pendingMessage.LocalMessageId, targetActorUid);

        System.Send(letter);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Обработчик входящих сетевых сообщений.
    /// </summary>
    private async Task OnNetworkMessageReceived(string sourceNodeName, string serializedMessage)
    {
        if (_isShuttingDown)
        {
            System.Logger.LogWarning("NetworkActor is shutting down, network message dropped");
            return;
        }

        System.Logger.LogDebug("Received network message from node {SourceNode}", sourceNodeName);

        NetworkLetter? networkLetter = _serializer.Deserialize(serializedMessage);
        if (networkLetter == null)
        {
            System.Logger.LogWarning("Failed to deserialize network message");
            return;
        }

        _nodeRegistry.RegisterNode(sourceNodeName, sourceNodeName);
        _nodeRegistry.SetNodeStatus(sourceNodeName, NodeStatus.Active);

        var payloadType = Type.GetType(networkLetter.PayloadTypeName, false);
        if (payloadType == null && _topologyAssembly != null)
        {
            var simpleName = networkLetter.PayloadTypeName.Split(',')[0];
            payloadType = _topologyAssembly.GetType(simpleName);
        }

        if (payloadType == null)
        {
            System.Logger.LogWarning("Failed to resolve payload type: {PayloadTypeName}", networkLetter.PayloadTypeName);
            return;
        }

        object payload;
        try
        {
            MethodInfo method = typeof(NetworkLetter).GetMethod(nameof(NetworkLetter.GetPayload));
            MethodInfo genericMethod = method!.MakeGenericMethod(payloadType);
            payload = genericMethod.Invoke(networkLetter, null)!;
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Failed to deserialize payload of type {PayloadType}", payloadType.Name);
            return;
        }

        var letterType = Type.GetType(networkLetter.LetterTypeName, false);
        if (letterType == null && _topologyAssembly != null)
        {
            var simpleName = networkLetter.LetterTypeName.Split(',')[0];
            letterType = _topologyAssembly.GetType(simpleName);
        }

        if (letterType == null)
        {
            System.Logger.LogWarning("Failed to resolve letter type: {LetterTypeName}", networkLetter.LetterTypeName);
            return;
        }

        var pendingMessage = new PendingMessage(networkLetter.LocalLetterId, ThisNodeName)
        {
            Payload = payload,
            PayloadType = payloadType,
            LetterType = letterType,
            SourceNode = networkLetter.SourceNodeName,
            CreatedAt = DateTime.UtcNow
        };

        await DeliverLocally(pendingMessage);
    }

    /// <summary>
    /// Обработчик обнаружения нового узла.
    /// </summary>
    private async Task OnNodeDiscovered(string nodeName, string nodeAddress)
    {
        System.Logger.LogInformation("New node discovered: {NodeName} -> {NodeAddress}", nodeName, nodeAddress);

        var isNew = _nodeRegistry.RegisterNode(nodeName, nodeAddress);
        _nodeRegistry.SetNodeStatus(nodeName, NodeStatus.Active);

        if (isNew)
        {
            await FlushPendingLettersForNode(nodeName);
        }
    }

    /// <summary>
    /// Отправляет все отложенные письма для указанного узла.
    /// </summary>
    private async Task FlushPendingLettersForNode(string nodeName)
    {
        IReadOnlyList<PendingMessage> lettersToSend = _pendingQueue.DequeueForNode(nodeName);

        if (!lettersToSend.Any())
        {
            return;
        }

        System.Logger.LogInformation("Sending {Count} pending letters for node {Node}", lettersToSend.Count, nodeName);

        foreach (PendingMessage letter in lettersToSend)
        {
            await SendLetter(letter);
        }
    }

    /// <summary>
    /// Отменяет отправку письма по локальному идентификатору.
    /// </summary>
    /// <param name="localMessageId">Локальный идентификатор сообщения.</param>
    /// <returns>true, если сообщение найдено и удалено, иначе false.</returns>
    public bool CancelPendingLetter(Guid localMessageId)
    {
        if (_pendingQueue.Remove(localMessageId))
        {
            System.Logger.LogDebug("Letter {LetterId} removed from pending queue", localMessageId);
            return true;
        }

        System.Logger.LogDebug("Letter {LetterId} not found in pending queue", localMessageId);
        return false;
    }

    /// <summary>
    /// Завершение работы сетевого актора.
    /// </summary>
    protected override async ValueTask OnShutdown()
    {
        _isShuttingDown = true;
        System.Logger.LogInformation("NetworkActor is shutting down");

        _pendingQueue.Clear();
        _nodeRegistry.Clear();
        _routerClient?.Dispose();
        _transport?.Dispose();

        await base.OnShutdown();
    }
}
