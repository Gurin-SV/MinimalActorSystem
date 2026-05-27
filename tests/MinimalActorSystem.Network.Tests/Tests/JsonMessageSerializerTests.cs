using MinimalActorSystem.Network.TestTopology;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для JsonMessageSerializer.
/// </summary>
public sealed class JsonMessageSerializerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly JsonMessageSerializer _serializer = new();

    #region Test Helpers

    private static NetworkLetter CreateTestNetworkLetter()
    {
        ComputePayload payload = new() { Number = 42, Operation = "square" };

        return NetworkLetter.Create(
            TestNodeNames.ComputeNode,
            TestNodeNames.TestNode,
            payload,
            typeof(ComputePayload),
            typeof(ComputeLetter),
            Guid.NewGuid());
    }

    #endregion

    /// <summary>
    /// Проверка: Serialize и Deserialize сохраняют все свойства.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_001()
    {
        _output.WriteLine("Test 001: Serialize and Deserialize preserve all properties");

        // Arrange
        NetworkLetter original = CreateTestNetworkLetter();

        // Act
        var serialized = _serializer.Serialize(original);
        NetworkLetter? deserialized = _serializer.Deserialize(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.DestinationNodeName, deserialized!.DestinationNodeName);
        Assert.Equal(original.SourceNodeName, deserialized.SourceNodeName);
        Assert.Equal(original.PayloadTypeName, deserialized.PayloadTypeName);
        Assert.Equal(original.LetterTypeName, deserialized.LetterTypeName);
        Assert.Equal(original.LocalLetterId, deserialized.LocalLetterId);

        _output.WriteLine($"Serialized length: {serialized.Length}");
        _output.WriteLine($"Destination: {deserialized.DestinationNodeName}");
        _output.WriteLine($"Source: {deserialized.SourceNodeName}");
        _output.WriteLine($"LocalLetterId: {deserialized.LocalLetterId}");
    }

    /// <summary>
    /// Проверка: Deserialize возвращает null для некорректных данных.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_002()
    {
        _output.WriteLine("Test 002: Deserialize returns null for invalid data");

        // Act
        NetworkLetter? result = _serializer.Deserialize("not a valid message");

        // Assert
        Assert.Null(result);
        _output.WriteLine("Result is null as expected");
    }

    /// <summary>
    /// Проверка: Deserialize возвращает null для строки без разделителя \0.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_003()
    {
        _output.WriteLine("Test 003: Deserialize returns null for string without separator");

        // Act
        NetworkLetter? result = _serializer.Deserialize("SomeDataWithoutNullSeparator");

        // Assert
        Assert.Null(result);
        _output.WriteLine("Result is null as expected");
    }

    /// <summary>
    /// Проверка: Deserialize возвращает null для неизвестного типа.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_004()
    {
        _output.WriteLine("Test 004: Deserialize returns null for unknown type");

        // Arrange
        var unknownTypeData = "Unknown.Type.Name, UnknownAssembly\0{}";

        // Act
        NetworkLetter? result = _serializer.Deserialize(unknownTypeData);

        // Assert
        Assert.Null(result);
        _output.WriteLine("Result is null as expected");
    }

    /// <summary>
    /// Проверка: Serialize генерирует строку, начинающуюся с AssemblyQualifiedName NetworkLetter.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_005()
    {
        _output.WriteLine("Test 005: Serialize generates string with NetworkLetter AssemblyQualifiedName prefix");

        // Arrange
        NetworkLetter letter = CreateTestNetworkLetter();

        // Act
        var serialized = _serializer.Serialize(letter);

        // Assert
        var expectedPrefix = typeof(NetworkLetter).AssemblyQualifiedName;
        Assert.NotNull(expectedPrefix);
        Assert.StartsWith(expectedPrefix + "\0", serialized);

        _output.WriteLine($"Expected prefix: {expectedPrefix}");
        _output.WriteLine($"Serialized starts with expected prefix: {serialized.StartsWith(expectedPrefix + "\0")}");
    }

    /// <summary>
    /// Проверка: Сериализация и десериализация с пустыми строками.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_006()
    {
        _output.WriteLine("Test 006: Serialize and deserialize with empty strings");

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
        var serialized = _serializer.Serialize(original);
        NetworkLetter? deserialized = _serializer.Deserialize(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("", deserialized!.DestinationNodeName);
        Assert.Equal("", deserialized.SourceNodeName);
        Assert.Equal(Guid.Empty, deserialized.LocalLetterId);

        SimpleTestPayload restoredPayload = deserialized.GetPayload<SimpleTestPayload>();
        Assert.Equal("test", restoredPayload.Text);
        Assert.Equal(1, restoredPayload.Counter);

        _output.WriteLine("Empty strings preserved correctly");
    }

    /// <summary>
    /// Проверка: Множественные сериализации/десериализации подряд.
    /// </summary>
    [Fact]
    public void JsonMessageSerializerTests_007()
    {
        _output.WriteLine("Test 007: Multiple serializations and deserializations");

        // Arrange
        NetworkLetter original = CreateTestNetworkLetter();

        // Act
        var serialized1 = _serializer.Serialize(original);
        NetworkLetter? deserialized1 = _serializer.Deserialize(serialized1);
        var serialized2 = _serializer.Serialize(deserialized1!);
        NetworkLetter? deserialized2 = _serializer.Deserialize(serialized2);

        // Assert
        Assert.NotNull(deserialized1);
        Assert.NotNull(deserialized2);
        Assert.Equal(original.DestinationNodeName, deserialized2!.DestinationNodeName);
        Assert.Equal(original.SourceNodeName, deserialized2.SourceNodeName);
        Assert.Equal(original.LocalLetterId, deserialized2.LocalLetterId);

        _output.WriteLine("Multiple serialization cycles preserved data correctly");
    }
}
