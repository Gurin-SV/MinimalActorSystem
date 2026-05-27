namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для PendingMessageQueue.
/// </summary>
public sealed class PendingMessageQueueTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region Test Helpers

    private static PendingMessage CreateTestMessage(string destinationNode = "TestNode")
    {
        return new PendingMessage(Guid.NewGuid(), destinationNode)
        {
            Payload = new object(),
            PayloadType = typeof(object),
            LetterType = typeof(Letter),
            SourceNode = "SourceNode",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    /// <summary>
    /// Проверка: Enqueue добавляет сообщение в очередь.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_001()
    {
        _output.WriteLine("Test 001: Enqueue adds message to queue");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message = CreateTestMessage("NodeA");

        // Act
        queue.Enqueue(message);

        // Assert
        Assert.Equal(1, queue.Count);
        Assert.True(queue.Contains(message.LocalMessageId));
        Assert.Equal(1, queue.GetQueuedCountForNode("NodeA"));
    }

    /// <summary>
    /// Проверка: Enqueue добавляет несколько сообщений для разных узлов.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_002()
    {
        _output.WriteLine("Test 002: Enqueue adds multiple messages for different nodes");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message1 = CreateTestMessage("NodeA");
        PendingMessage message2 = CreateTestMessage("NodeA");
        PendingMessage message3 = CreateTestMessage("NodeB");

        // Act
        queue.Enqueue(message1);
        queue.Enqueue(message2);
        queue.Enqueue(message3);

        // Assert
        Assert.Equal(3, queue.Count);
        Assert.Equal(2, queue.GetQueuedCountForNode("NodeA"));
        Assert.Equal(1, queue.GetQueuedCountForNode("NodeB"));
    }

    /// <summary>
    /// Проверка: DequeueForNode извлекает все сообщения для указанного узла.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_003()
    {
        _output.WriteLine("Test 003: DequeueForNode retrieves all messages for a node");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message1 = CreateTestMessage("NodeA");
        PendingMessage message2 = CreateTestMessage("NodeA");
        PendingMessage message3 = CreateTestMessage("NodeB");

        queue.Enqueue(message1);
        queue.Enqueue(message2);
        queue.Enqueue(message3);

        // Act
        IReadOnlyList<PendingMessage> messages = queue.DequeueForNode("NodeA");

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.Contains(message1.LocalMessageId, messages.Select(m => m.LocalMessageId));
        Assert.Contains(message2.LocalMessageId, messages.Select(m => m.LocalMessageId));
        Assert.Equal(1, queue.Count);
        Assert.Equal(0, queue.GetQueuedCountForNode("NodeA"));
        Assert.Equal(1, queue.GetQueuedCountForNode("NodeB"));
    }

    /// <summary>
    /// Проверка: DequeueForNode возвращает пустой список, если нет сообщений для узла.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_004()
    {
        _output.WriteLine("Test 004: DequeueForNode returns empty list when no messages");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message = CreateTestMessage("NodeA");
        queue.Enqueue(message);

        // Act
        IReadOnlyList<PendingMessage> messages = queue.DequeueForNode("NonExistentNode");

        // Assert
        Assert.Empty(messages);
        Assert.Equal(1, queue.Count);
    }

    /// <summary>
    /// Проверка: Remove удаляет сообщение по идентификатору.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_005()
    {
        _output.WriteLine("Test 005: Remove removes message by id");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message = CreateTestMessage("NodeA");
        queue.Enqueue(message);

        // Act
        var removed = queue.Remove(message.LocalMessageId);

        // Assert
        Assert.True(removed);
        Assert.Equal(0, queue.Count);
        Assert.False(queue.Contains(message.LocalMessageId));
        Assert.Equal(0, queue.GetQueuedCountForNode("NodeA"));
    }

    /// <summary>
    /// Проверка: Remove возвращает false для несуществующего сообщения.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_006()
    {
        _output.WriteLine("Test 006: Remove returns false for non-existent message");

        // Arrange
        PendingMessageQueue queue = new();

        // Act
        var removed = queue.Remove(Guid.NewGuid());

        // Assert
        Assert.False(removed);
        Assert.Equal(0, queue.Count);
    }

    /// <summary>
    /// Проверка: Get возвращает сообщение по идентификатору.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_007()
    {
        _output.WriteLine("Test 007: Get retrieves message by id");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message = CreateTestMessage("NodeA");
        queue.Enqueue(message);

        // Act
        PendingMessage? retrieved = queue.Get(message.LocalMessageId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(message.LocalMessageId, retrieved!.LocalMessageId);
        Assert.Equal(message.DestinationNode, retrieved.DestinationNode);
    }

    /// <summary>
    /// Проверка: Get возвращает null для несуществующего сообщения.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_008()
    {
        _output.WriteLine("Test 008: Get returns null for non-existent message");

        // Arrange
        PendingMessageQueue queue = new();

        // Act
        PendingMessage? retrieved = queue.Get(Guid.NewGuid());

        // Assert
        Assert.Null(retrieved);
    }

    /// <summary>
    /// Проверка: Contains проверяет наличие сообщения.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_009()
    {
        _output.WriteLine("Test 009: Contains checks message existence");

        // Arrange
        PendingMessageQueue queue = new();
        PendingMessage message = CreateTestMessage("NodeA");
        queue.Enqueue(message);

        // Act & Assert
        Assert.True(queue.Contains(message.LocalMessageId));
        Assert.False(queue.Contains(Guid.NewGuid()));
    }

    /// <summary>
    /// Проверка: GetQueuedCountForNode возвращает количество отложенных сообщений для узла.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_010()
    {
        _output.WriteLine("Test 010: GetQueuedCountForNode returns pending count for node");

        // Arrange
        PendingMessageQueue queue = new();
        queue.Enqueue(CreateTestMessage("NodeA"));
        queue.Enqueue(CreateTestMessage("NodeA"));
        queue.Enqueue(CreateTestMessage("NodeB"));

        // Act
        var countA = queue.GetQueuedCountForNode("NodeA");
        var countB = queue.GetQueuedCountForNode("NodeB");
        var countC = queue.GetQueuedCountForNode("NodeC");

        // Assert
        Assert.Equal(2, countA);
        Assert.Equal(1, countB);
        Assert.Equal(0, countC);
    }

    /// <summary>
    /// Проверка: Clear очищает очередь.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_011()
    {
        _output.WriteLine("Test 011: Clear empties the queue");

        // Arrange
        PendingMessageQueue queue = new();
        queue.Enqueue(CreateTestMessage("NodeA"));
        queue.Enqueue(CreateTestMessage("NodeA"));
        queue.Enqueue(CreateTestMessage("NodeB"));

        // Act
        queue.Clear();

        // Assert
        Assert.Equal(0, queue.Count);
        Assert.Equal(0, queue.GetQueuedCountForNode("NodeA"));
        Assert.Equal(0, queue.GetQueuedCountForNode("NodeB"));
        Assert.False(queue.Contains(Guid.NewGuid()));
    }

    /// <summary>
    /// Проверка: Сообщения сохраняют все свойства.
    /// </summary>
    [Fact]
    public void PendingMessageQueueTests_012()
    {
        _output.WriteLine("Test 012: Messages preserve all properties");

        // Arrange
        PendingMessageQueue queue = new();
        Guid localId = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;

        PendingMessage message = new(localId, "DestinationNode")
        {
            Payload = new object(),
            PayloadType = typeof(string),
            LetterType = typeof(Letter),
            SourceNode = "SourceNode",
            CreatedAt = createdAt,
            AttemptCount = 3,
            IsQueued = true
        };

        // Act
        queue.Enqueue(message);
        PendingMessage? retrieved = queue.Get(localId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(localId, retrieved!.LocalMessageId);
        Assert.Equal("DestinationNode", retrieved.DestinationNode);
        Assert.Equal("SourceNode", retrieved.SourceNode);
        Assert.Equal(typeof(string), retrieved.PayloadType);
        Assert.Equal(typeof(Letter), retrieved.LetterType);
        Assert.Equal(3, retrieved.AttemptCount);
        Assert.True(retrieved.IsQueued);
        Assert.Equal(createdAt, retrieved.CreatedAt);
    }
}
