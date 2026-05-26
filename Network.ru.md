# MinimalActorSystem.Network - проект распределённой акторной системы

## 1. Цели и принципы

Расширение `MinimalActorSystem` для поддержки сетевого взаимодействия без изменения самой базовой библиотеки. Локальные акторные системы работают на узлах сети и взаимодействуют между собой с минимизацией сетевого трафика.

**Ключевые принципы:**
- Сетевая логика полностью изолирована в отдельной сборке `MinimalActorSystem.Network`
- Сетевое взаимодействие строится по принципу "послал и забыл"
- Таймауты и ожидание ответов реализуются на уровне прикладных акторов через `ITimeService`
- Конкретные транспортные реализации (HTTP, gRPC, WebSockets) остаются за разработчиком приложения
- Сериализация — JSON, вынесенная в отдельный компонент

---

## 2. Архитектура системы

### 2.1 Компоненты

| Компонент | Описание |
|-----------|----------|
| **Узел (Node)** | Локальная акторная система + сетевой актор + web-сервис узла (опционально) |
| **Сетевой актор (NetworkActor)** | Единственный экземпляр на узел, `Uid = SystemUids.Network`. Диспетчер входящих/исходящих сетевых сообщений. Имеет две очереди: локальных писем и входящих сетевых сообщений. |
| **Роутер (Router)** | Web-сервис разрешения имён. Поддерживает таблицу `имя узла → сетевой адрес`, периодически проверяет доступность узлов через `/health`. Роутеры не взаимодействуют друг с другом. |
| **Топология (Topology)** | Общая сборка для всех узлов. Содержит DTO (Payload), атрибуты маршрутизации, списки имён узлов и роутеров. |

### 2.2 Поток данных

**Отправка:**

    Локальный актор → IPayloadLetter (Receiver = SystemUids.Network) → NetworkActor →
    → выбор узла по атрибутам [DestinationNode] → NetworkLetter → IMessageSerializer → INetworkTransport

**Получение:**

    INetworkTransport → IMessageSerializer → NetworkLetter → NetworkActor →
    → выбор локального актора по атрибуту/регистрации → создание оригинального письма (Sender = SystemUids.Network) →
    → System.Send() → целевой локальный актор

---

## 3. Сборка MinimalActorSystem.Network

### 3.1 NetworkActor (абстрактный базовый класс)

    public abstract class NetworkActor : Actor
    {
        protected NetworkActor(IActorSystem system, IEnumerable<string> routerAddresses, int queueCapacity = 512);
        
        public async Task InitializeAsync(
            string nodeName,
            string nodeAddress,
            INetworkTransport transport,
            IRouterClient routerClient,
            Assembly topologyAssembly);
        
        // Реестр узлов сети
        protected NodeRegistry NodeRegistry { get; }
        
        // Словари маршрутизации
        protected readonly Dictionary<Type, string[]> OutboundRoutes;
        protected readonly Dictionary<Type, Guid> InboundRoutes;
        
        // Регистрация исходящего маршрута для типа Payload
        protected void RegisterOutboundRoute<TPayload>(params string[] destinationNodeNames);
        protected void RegisterOutboundRoute(Type payloadType, params string[] destinationNodeNames);
        
        // Регистрация входящего маршрута для типа Payload
        protected void RegisterInboundRoute<TPayload>(Guid localActorUid);
        
        // Установка стратегии выбора узла
        public void SetNodeSelectionStrategy(INodeSelectionStrategy strategy);
        
        // Отмена отложенного письма по локальному идентификатору
        public bool CancelPendingLetter(Guid localMessageId);
    }

**Особенности:**
- Имеет две очереди (письма от локальных акторов + входящие сетевые сообщения)
- При отправке использует атрибуты `[DestinationNode]` на типе Payload (автоматическая загрузка через TopologyLoader)
- При получении использует `RegisterInboundRoute()` или атрибуты `[HandlesPayload]` для определения целевого актора
- Поддерживает очередь отложенных сообщений (`IPendingMessageQueue`)
- Автоматически загружает маршруты из атрибутов при инициализации

### 3.2 NodeRegistry (реестр узлов)

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

### 3.3 IPendingMessageQueue (очередь отложенных сообщений)

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

### 3.4 INodeSelectionStrategy (стратегия выбора узла)

    public interface INodeSelectionStrategy
    {
        string? SelectNode(string[] availableNodes, NodeRegistry nodeRegistry, string currentNodeName);
    }
    
    // Встроенные стратегии:
    public sealed class FirstAvailableStrategy : INodeSelectionStrategy;
    public sealed class LeastLoadedStrategy : INodeSelectionStrategy;
    public sealed class RoundRobinStrategy : INodeSelectionStrategy;

### 3.5 IMessageSerializer (сериализатор сообщений)

    public interface IMessageSerializer
    {
        string Serialize(NetworkLetter letter);
        NetworkLetter? Deserialize(string data);
    }
    
    public sealed class JsonMessageSerializer : IMessageSerializer;

**Формат сериализации:** `"TypeName\0{...json...}"`, где `TypeName` — AssemblyQualifiedName типа `NetworkLetter`.

### 3.6 TopologyLoader (загрузчик топологии)

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

### 3.7 INetworkTransport (интерфейс)

    public interface INetworkTransport : IDisposable
    {
        Task RegisterNodeAsync(string nodeName, CancellationToken cancellationToken = default);
        void SetMessageHandler(Func<string, string, Task> onMessageReceived);
        Task SendAsync(string nodeName, string serializedMessage, CancellationToken cancellationToken = default);
    }

### 3.8 IRouterClient (интерфейс)

    public interface IRouterClient : IDisposable
    {
        Task RegisterNodeAsync(string nodeName, string nodeAddress, CancellationToken cancellationToken = default);
        Task<string?> ResolveNodeAddressAsync(string nodeName, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, string>> GetAllNodesAsync(CancellationToken cancellationToken = default);
        void SetNodeDiscoveredHandler(Func<string, string, Task> onNodeDiscovered);
        Task StartPollingAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default);
    }

### 3.9 IPayloadLetter (интерфейс)

    public interface IPayloadLetter
    {
        object Payload { get; set; }
        Type PayloadType { get; }
    }

### 3.10 NetworkLetter (сериализуемый контейнер)

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

### 3.11 Атрибуты маршрутизации

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

## 4. Сборка Topology (общая для всех узлов)

Содержит:
- DTO-классы (Payload), помеченные атрибутами `[DestinationNode]` (можно несколько)
- Классы писем, реализующие `IPayloadLetter`
- Константы: имена узлов, адреса роутеров

### 4.1 Пример

    // Payload (DTO)
    [DestinationNode("ComputeNode")]
    [DestinationNode("BackupComputeNode")]
    public class PrimeCalculationPayload
    {
        public int Number { get; set; }
    }
    
    // Письмо-носитель
    public sealed class PrimeCalculationLetter : Letter, IPayloadLetter
    {
        public PrimeCalculationLetter(Guid sender, Guid receiver) : base(sender, receiver) { }
        public object Payload { get; set; }
        public Type PayloadType => typeof(PrimeCalculationPayload);
    }

---

## 5. Маршрутизация

### 5.1 Отправка (локальный → удалённый)

- `NetworkActor` автоматически загружает маршруты из атрибутов `[DestinationNode]` через `TopologyLoader`
- Если указано несколько узлов — выбирает один с помощью стратегии (по умолчанию `FirstAvailableStrategy`)
- Стратегию можно изменить через `SetNodeSelectionStrategy()`
- При отсутствии узла в реестре — письмо помещается в очередь отложенных

### 5.2 Получение (удалённый → локальный)

**Вариант A (декларативный, через атрибуты):**

    public class ComputeNodeNetworkActor : NetworkActor
    {
        public ComputeNodeNetworkActor(IActorSystem system, IEnumerable<string> routerAddresses)
            : base(system, routerAddresses) { }
    
        [HandlesPayload(typeof(PrimeCalculationPayload))]
        public Guid PrimeCalculatorHandler => ComputeNodeUids.PrimeCalculator;
    }

**Вариант B (императивный, через код после инициализации):**

    var actor = new ComputeNodeNetworkActor(system, routerAddresses);
    await actor.InitializeAsync(nodeName, nodeAddress, transport, routerClient, topologyAssembly);
    actor.AddOutboundRoute<PrimeCalculationPayload>("ComputeNode");
    actor.AddInboundRoute<PrimeCalculationPayload>(ComputeNodeUids.PrimeCalculator);

---

## 6. Обработка отказов и таймаутов

### 6.1 Отложенные сообщения

- Если узел назначения отсутствует в реестре, `NetworkActor` сохраняет `PendingMessage` в **очередь отложенных** (`IPendingMessageQueue`)
- При получении от роутера уведомления о появлении нового узла — отправляет все накопленные письма для этого узла

### 6.2 Таймауты и отмена

- Актор, ожидающий ответа, регистрирует таймаут через `ITimeService`
- Каждому отправленному письму присваивается локальный `Guid` (не передаётся по сети)
- При таймауте актор отправляет `NetworkActor` специальное письмо с этим `Guid`
- `NetworkActor` находит письмо в очереди отложенных и удаляет его через `CancelPendingLetter`

---

## 7. Старт узла и регистрация

1. Узел стартует, создаёт `ActorSystem` и `NetworkActor`
2. Вызывает `InitializeAsync` с именем узла, адресом, транспортом, клиентом роутера и сборкой топологии
3. Внутри `InitializeAsync`:
   - Регистрирует узел в транспорте через `_transport.RegisterNodeAsync(nodeName)`
   - Устанавливает обработчик сообщений через `_transport.SetMessageHandler(OnNetworkMessageReceived)`
   - Регистрирует узел во всех роутерах через `_routerClient.RegisterNodeAsync(nodeName, nodeAddress)`
   - Загружает все известные узлы через `_routerClient.GetAllNodesAsync()` в `NodeRegistry`
   - Устанавливает обработчик обнаружения узлов через `_routerClient.SetNodeDiscoveredHandler(OnNodeDiscovered)`
   - Запускает опрос роутера через `_routerClient.StartPollingAsync(TimeSpan.FromSeconds(30))`
   - Автоматически загружает исходящие маршруты из атрибутов через `TopologyLoader`
   - Автоматически загружает входящие маршруты из атрибутов `[HandlesPayload]`
4. Роутеры начинают периодически опрашивать узел через `/health` (зависит от реализации)
5. Если узел хочет отправить сообщение, но узел-получатель отсутствует в реестре — письмо уходит в очередь отложенных
6. При получении от роутера успешного разрешения (новый узел) — отправляются накопленные письма

---

## 8. Взаимодействие роутеров

- Роутеры **не обмениваются данными** друг с другом
- Каждый роутер независимо ведёт свою таблицу имён → адресов
- Узел может получить несколько вариантов адреса для одного имени (если роутеры расходятся во мнении)
- Выбор конкретного адреса — ответственность транспорта

---

## 9. Ответственность разработчика приложения

| Компонент | Где реализует разработчик |
|-----------|---------------------------|
| Транспорт (`INetworkTransport`) | Полностью, под свои стандарты |
| Клиент роутера (`IRouterClient`) | Полностью (HTTP/gRPC к своим роутерам) |
| Web-сервисы узлов и роутеров | Полностью |
| Сборка `Topology` | Полностью |
| Конкретный `NetworkActor` | Наследуется от `NetworkActor`, добавляет обработчики через атрибуты `[HandlesPayload]` |
| Безопасность, аутентификация | На уровне транспорта и роутеров |

---

## 10. Неломающие изменения в MinimalActorSystem

В `SystemUids` добавляется константа:

    public static readonly Guid Network = new("00000000-0000-0000-0000-000000000003");

Никаких других изменений в базовую библиотеку не вносится.

---

## 11. Итоговая схема зависимостей сборок

    MinimalActorSystem (ядро)
            ↑
    MinimalActorSystem.Network (сетевые абстракции)
            ↑
    Topology (Payload, атрибуты, константы)
            ↑
    NodeApplication (конкретный NetworkActor, транспорт, роутер-клиент)

---

## 12. Тестирование

Все компоненты покрыты модульными тестами. Для тестирования используются:
- `FakeNetworkTransport` — фейковая реализация транспорта
- `FakeRouterClient` — фейковая реализация клиента роутера
- `TestNetworkActor` — тестовая реализация сетевого актора
- `TestReceiverActor` — тестовый актор-получатель
- `TestTopology` — тестовая топология с примерами DTO и писем

**Количество тестов:** 89

**Статус:** Все тесты проходят
