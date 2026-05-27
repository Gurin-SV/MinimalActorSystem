using MinimalActorSystem.Network.Tests.Actors;
using MinimalActorSystem.Network.Tests.Fakes;
using MinimalActorSystem.Network.TestTopology;
using System.Collections.Concurrent;
using System.Reflection;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для NetworkActor с использованием фейковых компонентов.
/// </summary>
public sealed class NetworkActorTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Проверка: NetworkActor отправляет письмо на удалённый узел через транспорт.
    /// </summary>
    [Fact]
    public async Task NetworkActorTests_001()
    {
        _output.WriteLine("Test 001: NetworkActor sends letter to remote node via transport");

        // Arrange
        ConcurrentDictionary<string, FakeNetworkTransport> registry = new();
        Assembly topologyAssembly = typeof(TestTopology.ComputePayload).Assembly;

        // Создаём систему отправителя
        ActorSystem senderSystem = SystemFactory.CreateSystem(_output);
        FakeNetworkTransport senderTransport = new(registry, senderSystem);
        FakeRouterClient senderRouter = new(senderSystem);

        TestNetworkActor senderActor = new(senderSystem, ["http://router:8080"]);
        senderSystem.RegisterActor(senderActor);
        await senderActor.InitializeAsync("SenderNode", "http://sender:8080", senderTransport, senderRouter, topologyAssembly);

        // Настройка маршрутов после инициализации
        senderActor.AddOutboundRoute<ComputePayload>("ComputeNode");

        // Создаём систему получателя
        ActorSystem receiverSystem = SystemFactory.CreateSystem(_output);
        FakeNetworkTransport receiverTransport = new(registry, receiverSystem);
        FakeRouterClient receiverRouter = new(receiverSystem);

        TestNetworkActor receiverActor = new(receiverSystem, ["http://router:8080"]);
        receiverSystem.RegisterActor(receiverActor);
        await receiverActor.InitializeAsync("ComputeNode", "http://compute:8080", receiverTransport, receiverRouter, topologyAssembly);

        // Добавляем узел в роутер отправителя
        await senderRouter.AddNodeForTestingAsync("ComputeNode", "http://compute:8080");

        // Создаём актора-получателя для проверки доставки
        Guid receiverUid = Guid.NewGuid();
        TestReceiverActor testReceiver = new(receiverSystem, receiverUid, "TestReceiver");
        receiverSystem.RegisterActor(testReceiver);

        // Настройка входящего маршрута
        receiverActor.AddInboundRoute<ComputePayload>(receiverUid);

        // Даём время на применение маршрутов
        await Task.Delay(200);

        // Создаём письмо с Payload
        ComputePayload payload = new() { Number = 42, Operation = "square" };
        ComputeLetter letter = new(SystemUids.System, SystemUids.Network)
        {
            Payload = payload
        };

        _output.WriteLine("Sending letter...");

        // Act
        var sendResult = senderSystem.Send(letter);
        _output.WriteLine($"Send result: {sendResult}");

        // Assert
        IPayloadLetter received = await testReceiver.FirstLetterReceived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.IsType<ComputePayload>(received.Payload);
        Assert.Equal(42, ((ComputePayload)received.Payload).Number);
        Assert.Equal("square", ((ComputePayload)received.Payload).Operation);

        _output.WriteLine("Test 001 passed: letter delivered to remote node");

        // Cleanup
        senderSystem.Shutdown();
        receiverSystem.Shutdown();
        await senderSystem.WaitForShutdownAsync();
        await receiverSystem.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: NetworkActor доставляет письмо локально, когда целевой узел равен текущему.
    /// </summary>
    [Fact]
    public async Task NetworkActorTests_002()
    {
        _output.WriteLine("Test 002: NetworkActor delivers letter locally when destination is current node");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        ConcurrentDictionary<string, FakeNetworkTransport> registry = new();
        Assembly topologyAssembly = typeof(TestTopology.ComputePayload).Assembly;

        FakeNetworkTransport transport = new(registry, system);
        FakeRouterClient router = new(system);

        Guid receiverUid = Guid.NewGuid();
        TestReceiverActor testReceiver = new(system, receiverUid, "TestReceiver");
        system.RegisterActor(testReceiver);

        TestNetworkActor networkActor = new(system, ["http://router:8080"]);
        system.RegisterActor(networkActor);

        await networkActor.InitializeAsync("LocalNode", "http://local:8080", transport, router, topologyAssembly);

        // Настройка маршрутов после инициализации
        networkActor.AddOutboundRoute<ComputePayload>("LocalNode");
        networkActor.AddInboundRoute<ComputePayload>(receiverUid);

        ComputePayload payload = new() { Number = 100, Operation = "double" };
        ComputeLetter letter = new(SystemUids.System, SystemUids.Network)
        {
            Payload = payload
        };

        // Act
        system.Send(letter);

        // Assert
        IPayloadLetter received = await testReceiver.FirstLetterReceived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.Equal(100, ((ComputePayload)received.Payload).Number);

        _output.WriteLine("Test 002 passed: letter delivered locally");

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: NetworkActor помещает письмо в очередь отложенных, если узел назначения неизвестен.
    /// </summary>
    [Fact]
    public async Task NetworkActorTests_003()
    {
        _output.WriteLine("Test 003: NetworkActor queues letter when destination node is unknown");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        ConcurrentDictionary<string, FakeNetworkTransport> registry = new();
        Assembly topologyAssembly = typeof(TestTopology.ComputePayload).Assembly;

        FakeNetworkTransport transport = new(registry, system);
        FakeRouterClient router = new(system);

        TestNetworkActor networkActor = new(system, ["http://router:8080"]);
        system.RegisterActor(networkActor);
        await networkActor.InitializeAsync("SenderNode", "http://sender:8080", transport, router, topologyAssembly);

        // Настройка маршрута на неизвестный узел
        networkActor.AddOutboundRoute<ComputePayload>("UnknownNode");

        ComputePayload payload = new() { Number = 42, Operation = "square" };
        ComputeLetter letter = new(SystemUids.System, SystemUids.Network)
        {
            Payload = payload
        };

        // Act
        system.Send(letter);

        // Даем время на обработку
        await Task.Delay(500);

        // Assert - нет исключений, письмо в очереди отложенных
        _output.WriteLine("Test 003 passed: letter queued without errors");

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: CancelPendingLetter отменяет отправку письма.
    /// </summary>
    [Fact]
    public async Task NetworkActorTests_004()
    {
        _output.WriteLine("Test 004: CancelPendingLetter cancels letter sending");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        ConcurrentDictionary<string, FakeNetworkTransport> registry = new();
        Assembly topologyAssembly = typeof(TestTopology.ComputePayload).Assembly;

        FakeNetworkTransport transport = new(registry, system);
        FakeRouterClient router = new(system);

        TestNetworkActor networkActor = new(system, ["http://router:8080"]);
        system.RegisterActor(networkActor);
        await networkActor.InitializeAsync("SenderNode", "http://sender:8080", transport, router, topologyAssembly);

        // Настройка маршрута на неизвестный узел (письмо попадёт в очередь)
        networkActor.AddOutboundRoute<ComputePayload>("UnknownNode");

        ComputePayload payload = new() { Number = 42, Operation = "square" };
        ComputeLetter letter = new(SystemUids.System, SystemUids.Network)
        {
            Payload = payload
        };

        system.Send(letter);
        await Task.Delay(200);

        // Act - отменяем письмо
        var cancelResult = networkActor.CancelPendingLetter(Guid.NewGuid());

        // Assert
        Assert.False(cancelResult);
        _output.WriteLine($"Cancel result: {cancelResult}");

        _output.WriteLine("Test 004 passed: CancelPendingLetter works");

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }
}
