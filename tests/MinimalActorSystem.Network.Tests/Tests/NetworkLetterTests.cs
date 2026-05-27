using MinimalActorSystem.Network.TestTopology;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для класса NetworkLetter.
/// </summary>
public sealed class NetworkLetterTests
{
    private readonly JsonMessageSerializer _serializer = new();

    /// <summary>
    /// Проверка: Create создаёт NetworkLetter, Serialize/Deserialize сохраняют все свойства.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_001()
    {
        // Arrange
        Guid localId = Guid.NewGuid();
        ComputePayload originalPayload = new() { Number = 42, Operation = "square" };

        NetworkLetter original = NetworkLetter.Create(
            TestNodeNames.ComputeNode,
            TestNodeNames.TestNode,
            originalPayload,
            typeof(ComputePayload),
            typeof(ComputeLetter),
            localId);

        // Act
        string serialized = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.DestinationNodeName, deserialized!.DestinationNodeName);
        Assert.Equal(original.SourceNodeName, deserialized.SourceNodeName);
        Assert.Equal(original.PayloadTypeName, deserialized.PayloadTypeName);
        Assert.Equal(original.LetterTypeName, deserialized.LetterTypeName);
        Assert.Equal(original.LocalLetterId, deserialized.LocalLetterId);

        var restoredPayload = deserialized.GetPayload<ComputePayload>();
        Assert.Equal(42, restoredPayload.Number);
        Assert.Equal("square", restoredPayload.Operation);
    }

    /// <summary>
    /// Проверка: Deserialize с некорректными данными возвращает null.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_002()
    {
        // Arrange
        string invalidData = "not a valid network message";

        // Act
        var result = _serializer.Deserialize(invalidData);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверка: Deserialize с данными, не содержащими разделитель \0, возвращает null.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_003()
    {
        // Arrange
        string dataWithoutSeparator = "SomeDataWithoutNullSeparator";

        // Act
        var result = _serializer.Deserialize(dataWithoutSeparator);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверка: Deserialize с неизвестным типом в AssemblyQualifiedName возвращает null.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_004()
    {
        // Arrange
        string unknownTypeData = "Unknown.Type.Name, UnknownAssembly\0{}";

        // Act
        var result = _serializer.Deserialize(unknownTypeData);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверка: Serialize генерирует строку, начинающуюся с AssemblyQualifiedName NetworkLetter.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_005()
    {
        // Arrange
        SimpleTestPayload payload = new() { Text = "hello", Counter = 10 };

        NetworkLetter letter = NetworkLetter.Create(
            TestNodeNames.ComputeNode,
            TestNodeNames.TestNode,
            payload,
            typeof(SimpleTestPayload),
            typeof(SimpleTestLetter),
            Guid.NewGuid());

        // Act
        string serialized = _serializer.Serialize(letter);

        // Assert
        string? expectedPrefix = typeof(NetworkLetter).AssemblyQualifiedName;
        Assert.NotNull(expectedPrefix);
        Assert.StartsWith(expectedPrefix + "\0", serialized);
    }

    /// <summary>
    /// Проверка: Свойства PayloadType и LetterType восстанавливают типы из строк.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_006()
    {
        // Arrange
        var expectedPayloadType = typeof(StoreDataPayload);
        var expectedLetterType = typeof(StoreDataLetter);
        StoreDataPayload payload = new() { Key = "test", Value = "data" };

        NetworkLetter letter = NetworkLetter.Create(
            TestNodeNames.StorageNode,
            TestNodeNames.TestNode,
            payload,
            expectedPayloadType,
            expectedLetterType,
            Guid.NewGuid());

        // Act & Assert
        Assert.Equal(expectedPayloadType, letter.PayloadType);
        Assert.Equal(expectedLetterType, letter.LetterType);
    }

    /// <summary>
    /// Проверка: Сериализация и десериализация с пустыми строками в именах узлов.
    /// </summary>
    [Fact]
    public void NetworkLetterTests_007()
    {
        // Arrange
        SimpleTestPayload payload = new() { Text = "test", Counter = 1 };

        NetworkLetter original = NetworkLetter.Create(
            "",
            "",
            payload,
            typeof(SimpleTestPayload),
            typeof(SimpleTestLetter),
            Guid.Empty);

        // Act
        string serialized = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("", deserialized!.DestinationNodeName);
        Assert.Equal("", deserialized.SourceNodeName);
        Assert.Equal(Guid.Empty, deserialized.LocalLetterId);

        var restoredPayload = deserialized.GetPayload<SimpleTestPayload>();
        Assert.Equal("test", restoredPayload.Text);
        Assert.Equal(1, restoredPayload.Counter);
    }
}
