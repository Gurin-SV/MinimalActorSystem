namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для NodeRegistry.
/// </summary>
public sealed class NodeRegistryTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Проверка: RegisterNode добавляет новый узел.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_001()
    {
        _output.WriteLine("Test 001: RegisterNode adds new node");

        // Arrange
        var registry = new NodeRegistry();

        // Act
        var isNew = registry.RegisterNode("TestNode", "http://localhost:8080");

        // Assert
        Assert.True(isNew);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("TestNode"));
        Assert.True(registry.TryGetNodeAddress("TestNode", out var address));
        Assert.Equal("http://localhost:8080", address);
    }

    /// <summary>
    /// Проверка: RegisterNode обновляет существующий узел.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_002()
    {
        _output.WriteLine("Test 002: RegisterNode updates existing node");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("TestNode", "http://localhost:8080");

        // Act
        var isNew = registry.RegisterNode("TestNode", "http://localhost:9090");

        // Assert
        Assert.False(isNew);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGetNodeAddress("TestNode", out var address));
        Assert.Equal("http://localhost:9090", address);
    }

    /// <summary>
    /// Проверка: UnregisterNode удаляет узел.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_003()
    {
        _output.WriteLine("Test 003: UnregisterNode removes node");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("TestNode", "http://localhost:8080");

        // Act
        var removed = registry.UnregisterNode("TestNode");

        // Assert
        Assert.True(removed);
        Assert.Equal(0, registry.Count);
        Assert.False(registry.Contains("TestNode"));
    }

    /// <summary>
    /// Проверка: TryGetNodeAddress возвращает false для неизвестного узла.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_004()
    {
        _output.WriteLine("Test 004: TryGetNodeAddress returns false for unknown node");

        // Arrange
        var registry = new NodeRegistry();

        // Act
        var found = registry.TryGetNodeAddress("UnknownNode", out var address);

        // Assert
        Assert.False(found);
        Assert.Null(address);
    }

    /// <summary>
    /// Проверка: GetNodeInfo возвращает информацию об узле.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_005()
    {
        _output.WriteLine("Test 005: GetNodeInfo returns node information");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("TestNode", "http://localhost:8080");

        // Act
        var info = registry.GetNodeInfo("TestNode");

        // Assert
        Assert.NotNull(info);
        Assert.Equal("TestNode", info!.NodeName);
        Assert.Equal("http://localhost:8080", info.NodeAddress);
        Assert.Equal(NodeStatus.Inactive, info.Status);
    }

    /// <summary>
    /// Проверка: SetNodeStatus изменяет статус узла.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_006()
    {
        _output.WriteLine("Test 006: SetNodeStatus changes node status");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("TestNode", "http://localhost:8080");

        // Act
        var updated = registry.SetNodeStatus("TestNode", NodeStatus.Active);
        var info = registry.GetNodeInfo("TestNode");

        // Assert
        Assert.True(updated);
        Assert.Equal(NodeStatus.Active, info!.Status);
    }

    /// <summary>
    /// Проверка: GetActiveNodes возвращает только активные узлы.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_007()
    {
        _output.WriteLine("Test 007: GetActiveNodes returns only active nodes");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("Node1", "http://localhost:8081");
        registry.RegisterNode("Node2", "http://localhost:8082");
        registry.RegisterNode("Node3", "http://localhost:8083");

        registry.SetNodeStatus("Node1", NodeStatus.Active);
        registry.SetNodeStatus("Node3", NodeStatus.Active);

        // Act
        var activeNodes = registry.GetActiveNodes();

        // Assert
        Assert.Equal(2, activeNodes.Count);
        Assert.Contains(activeNodes, n => n.NodeName == "Node1");
        Assert.Contains(activeNodes, n => n.NodeName == "Node3");
    }

    /// <summary>
    /// Проверка: GetAllNodes возвращает все узлы.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_008()
    {
        _output.WriteLine("Test 008: GetAllNodes returns all nodes");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("Node1", "http://localhost:8081");
        registry.RegisterNode("Node2", "http://localhost:8082");

        // Act
        var allNodes = registry.GetAllNodes();

        // Assert
        Assert.Equal(2, allNodes.Count);
    }

    /// <summary>
    /// Проверка: Clear очищает реестр.
    /// </summary>
    [Fact]
    public void NodeRegistryTests_009()
    {
        _output.WriteLine("Test 009: Clear empties the registry");

        // Arrange
        var registry = new NodeRegistry();
        registry.RegisterNode("Node1", "http://localhost:8081");
        registry.RegisterNode("Node2", "http://localhost:8082");

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.Contains("Node1"));
        Assert.False(registry.Contains("Node2"));
    }
}
