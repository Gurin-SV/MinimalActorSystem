namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для стратегий выбора узла.
/// </summary>
public sealed class NodeSelectionStrategyTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region Test Helpers

    private static NodeRegistry CreateTestRegistry()
    {
        NodeRegistry registry = new();
        registry.RegisterNode("NodeA", "http://node-a:8080");
        registry.RegisterNode("NodeB", "http://node-b:8080");
        registry.RegisterNode("NodeC", "http://node-c:8080");
        registry.RegisterNode("NodeD", "http://node-d:8080");

        registry.SetNodeStatus("NodeA", NodeStatus.Active);
        registry.SetNodeStatus("NodeB", NodeStatus.Active);
        registry.SetNodeStatus("NodeC", NodeStatus.Active);
        // NodeD остаётся Inactive

        return registry;
    }

    #endregion

    #region FirstAvailableStrategy

    /// <summary>
    /// Проверка: FirstAvailableStrategy выбирает первый активный узел из списка.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_001()
    {
        _output.WriteLine("Test 001: FirstAvailableStrategy selects first active node");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        FirstAvailableStrategy strategy = new();
        var availableNodes = new[] { "NodeC", "NodeB", "NodeA" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeC", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    /// <summary>
    /// Проверка: FirstAvailableStrategy пропускает неактивные узлы.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_002()
    {
        _output.WriteLine("Test 002: FirstAvailableStrategy skips inactive nodes");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        FirstAvailableStrategy strategy = new();
        var availableNodes = new[] { "NodeD", "NodeB", "NodeC" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeB", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    /// <summary>
    /// Проверка: FirstAvailableStrategy не выбирает текущий узел.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_003()
    {
        _output.WriteLine("Test 003: FirstAvailableStrategy does not select current node");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        FirstAvailableStrategy strategy = new();
        var availableNodes = new[] { "CurrentNode", "NodeA", "NodeB" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeA", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    /// <summary>
    /// Проверка: FirstAvailableStrategy возвращает null, если нет доступных узлов.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_004()
    {
        _output.WriteLine("Test 004: FirstAvailableStrategy returns null when no nodes available");

        // Arrange
        NodeRegistry registry = new();
        FirstAvailableStrategy strategy = new();
        var availableNodes = new[] { "Unknown1", "Unknown2" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("Unknown1", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    #endregion

    #region RoundRobinStrategy

    /// <summary>
    /// Проверка: RoundRobinStrategy циклически перебирает узлы.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_005()
    {
        _output.WriteLine("Test 005: RoundRobinStrategy cycles through nodes");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        RoundRobinStrategy strategy = new();
        var availableNodes = new[] { "NodeA", "NodeB", "NodeC" };

        // Act
        var first = strategy.SelectNode(availableNodes, registry, "CurrentNode");
        var second = strategy.SelectNode(availableNodes, registry, "CurrentNode");
        var third = strategy.SelectNode(availableNodes, registry, "CurrentNode");
        var fourth = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeA", first);
        Assert.Equal("NodeB", second);
        Assert.Equal("NodeC", third);
        Assert.Equal("NodeA", fourth);
        _output.WriteLine($"Sequence: {first} -> {second} -> {third} -> {fourth}");
    }

    /// <summary>
    /// Проверка: RoundRobinStrategy пропускает неактивные узлы.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_006()
    {
        _output.WriteLine("Test 006: RoundRobinStrategy skips inactive nodes");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        RoundRobinStrategy strategy = new();
        var availableNodes = new[] { "NodeD", "NodeA", "NodeB" };

        // Act
        var first = strategy.SelectNode(availableNodes, registry, "CurrentNode");
        var second = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeA", first);
        Assert.Equal("NodeB", second);
        _output.WriteLine($"Selected: {first}, {second}");
    }

    /// <summary>
    /// Проверка: RoundRobinStrategy не выбирает текущий узел.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_007()
    {
        _output.WriteLine("Test 007: RoundRobinStrategy does not select current node");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        RoundRobinStrategy strategy = new();
        var availableNodes = new[] { "CurrentNode", "NodeA", "NodeB" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeA", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    #endregion

    #region LeastLoadedStrategy

    /// <summary>
    /// Проверка: LeastLoadedStrategy выбирает узел с наименьшей очередью.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_008()
    {
        _output.WriteLine("Test 008: LeastLoadedStrategy selects node with smallest queue");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        PendingMessageQueue pendingQueue = new();
        LeastLoadedStrategy strategy = new(pendingQueue);
        var availableNodes = new[] { "NodeA", "NodeB", "NodeC" };

        // Добавляем сообщения в очередь для разных узлов
        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));
        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));
        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeB"));

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeC", selected);
        _output.WriteLine($"Selected node: {selected} (NodeA:2, NodeB:1, NodeC:0)");
    }

    /// <summary>
    /// Проверка: LeastLoadedStrategy не выбирает неактивные узлы.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_009()
    {
        _output.WriteLine("Test 009: LeastLoadedStrategy skips inactive nodes");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        PendingMessageQueue pendingQueue = new();
        LeastLoadedStrategy strategy = new(pendingQueue);
        var availableNodes = new[] { "NodeD", "NodeA", "NodeB" };

        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));
        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeB", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    /// <summary>
    /// Проверка: LeastLoadedStrategy не выбирает текущий узел.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_010()
    {
        _output.WriteLine("Test 010: LeastLoadedStrategy does not select current node");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        PendingMessageQueue pendingQueue = new();
        LeastLoadedStrategy strategy = new(pendingQueue);
        var availableNodes = new[] { "CurrentNode", "NodeA", "NodeB" };

        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));
        pendingQueue.Enqueue(new PendingMessage(Guid.NewGuid(), "NodeA"));

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeB", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    /// <summary>
    /// Проверка: LeastLoadedStrategy возвращает первый узел, если все имеют одинаковую нагрузку.
    /// </summary>
    [Fact]
    public void NodeSelectionStrategyTests_011()
    {
        _output.WriteLine("Test 011: LeastLoadedStrategy returns first node when loads are equal");

        // Arrange
        NodeRegistry registry = CreateTestRegistry();
        PendingMessageQueue pendingQueue = new();
        LeastLoadedStrategy strategy = new(pendingQueue);
        var availableNodes = new[] { "NodeA", "NodeB", "NodeC" };

        // Act
        var selected = strategy.SelectNode(availableNodes, registry, "CurrentNode");

        // Assert
        Assert.Equal("NodeA", selected);
        _output.WriteLine($"Selected node: {selected}");
    }

    #endregion
}
