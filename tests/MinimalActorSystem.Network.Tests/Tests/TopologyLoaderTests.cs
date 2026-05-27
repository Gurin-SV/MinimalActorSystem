using MinimalActorSystem.Network.TestTopology;
using System.Reflection;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для TopologyLoader.
/// </summary>
public sealed class TopologyLoaderTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly Assembly _topologyAssembly = typeof(TestTopology.ComputePayload).Assembly;

    /// <summary>
    /// Проверка: Конструктор TopologyLoader сохраняет сборку.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_001()
    {
        _output.WriteLine("Test 001: Constructor saves assembly");

        // Act
        TopologyLoader loader = new(_topologyAssembly);

        // Assert
        Assert.Same(_topologyAssembly, loader.Assembly);
        _output.WriteLine($"Assembly: {loader.Assembly.FullName}");
    }

    /// <summary>
    /// Проверка: Constructor выбрасывает исключение при null.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_002()
    {
        _output.WriteLine("Test 002: Constructor throws on null");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TopologyLoader(null!));
    }

    /// <summary>
    /// Проверка: DestinationNodeRoutes загружает маршруты из атрибутов.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_003()
    {
        _output.WriteLine("Test 003: DestinationNodeRoutes loads routes from attributes");

        // Act
        TopologyLoader loader = new(_topologyAssembly);
        IReadOnlyDictionary<Type, string[]> routes = loader.DestinationNodeRoutes;

        // Assert
        Assert.NotEmpty(routes);

        foreach (KeyValuePair<Type, string[]> route in routes)
        {
            _output.WriteLine($"Payload: {route.Key.Name}, Nodes: {string.Join(", ", route.Value)}");
        }

        // Проверяем конкретные ожидаемые маршруты из TestTopology
        Assert.Contains(routes, r => r.Key == typeof(ComputePayload));
        Assert.Contains(routes, r => r.Key == typeof(ComputeResultPayload));
        Assert.Contains(routes, r => r.Key == typeof(StoreDataPayload));
        Assert.DoesNotContain(routes, r => r.Key == typeof(SimpleTestPayload)); // у этого нет атрибута
    }

    /// <summary>
    /// Проверка: RouterAddresses загружает адреса роутеров.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_004()
    {
        _output.WriteLine("Test 004: RouterAddresses loads router addresses");

        // Act
        TopologyLoader loader = new(_topologyAssembly);
        IReadOnlyList<string> addresses = loader.RouterAddresses;

        // Assert
        Assert.NotEmpty(addresses);

        foreach (var address in addresses)
        {
            _output.WriteLine($"Router address: {address}");
        }

        Assert.Contains("http://localhost:5001", addresses);
        Assert.Contains("http://localhost:5002", addresses);
    }

    /// <summary>
    /// Проверка: NodeNames загружает имена узлов.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_005()
    {
        _output.WriteLine("Test 005: NodeNames loads node names");

        // Act
        TopologyLoader loader = new(_topologyAssembly);
        IReadOnlyList<string> nodeNames = loader.NodeNames;

        // Assert
        Assert.NotEmpty(nodeNames);

        foreach (var name in nodeNames)
        {
            _output.WriteLine($"Node name: {name}");
        }

        Assert.Contains("ComputeNode", nodeNames);
        Assert.Contains("BackupComputeNode", nodeNames);
        Assert.Contains("StorageNode", nodeNames);
        Assert.Contains("TestNode", nodeNames);
    }

    /// <summary>
    /// Проверка: GetDestinationNodesForPayload возвращает маршруты для Payload.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_006()
    {
        _output.WriteLine("Test 006: GetDestinationNodesForPayload returns routes for Payload");

        // Arrange
        TopologyLoader loader = new(_topologyAssembly);

        // Act
        var computeNodes = loader.GetDestinationNodesForPayload(typeof(ComputePayload));
        var storeNodes = loader.GetDestinationNodesForPayload(typeof(StoreDataPayload));
        var simpleNodes = loader.GetDestinationNodesForPayload(typeof(SimpleTestPayload));

        // Assert
        Assert.NotNull(computeNodes);
        Assert.Contains("ComputeNode", computeNodes);
        Assert.Contains("BackupComputeNode", computeNodes);

        Assert.NotNull(storeNodes);
        Assert.Contains("StorageNode", storeNodes);

        Assert.Null(simpleNodes);

        _output.WriteLine($"ComputePayload -> {string.Join(", ", computeNodes)}");
        _output.WriteLine($"StoreDataPayload -> {string.Join(", ", storeNodes)}");
        _output.WriteLine($"SimpleTestPayload -> {(simpleNodes == null ? "null" : string.Join(", ", simpleNodes))}");
    }

    /// <summary>
    /// Проверка: GetPayloadTypes возвращает все типы с атрибутами DestinationNode.
    /// </summary>
    [Fact]
    public void TopologyLoaderTests_007()
    {
        _output.WriteLine("Test 007: GetPayloadTypes returns all types with DestinationNode attribute");

        // Arrange
        TopologyLoader loader = new(_topologyAssembly);

        // Act
        IReadOnlyList<Type> payloadTypes = loader.GetPayloadTypes();

        // Assert
        Assert.Equal(3, payloadTypes.Count); // ComputePayload, ComputeResultPayload, StoreDataPayload
        Assert.Contains(payloadTypes, t => t == typeof(ComputePayload));
        Assert.Contains(payloadTypes, t => t == typeof(ComputeResultPayload));
        Assert.Contains(payloadTypes, t => t == typeof(StoreDataPayload));

        foreach (Type type in payloadTypes)
        {
            _output.WriteLine($"Payload type with DestinationNode: {type.Name}");
        }
    }
}
