using System.Text;
using System.Text.Json;

namespace MinimalActorSystem.Network.Tests;

public sealed class NetworkLetterTests
{
    // Общие настройки сериализации для всех тестов в классе
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Проверка: сериализация и десериализация сохраняют данные
    /// </summary>
    [Fact]
    public void NetworkLetterTests_001()
    {
        // Arrange
        var payload = new TestPayload { Message = "Hello Network!" };
        var serializedContent = JsonSerializer.Serialize(payload, JsonOptions);
        var letterTypeName = typeof(TestPayload).FullName ?? "TestPayload";
        var targetNode = "TargetNode";

        var originalLetter = new DefaultNetworkLetter(
            receiverNodeName: targetNode,
            serializedLetter: serializedContent,
            letterTypeName: letterTypeName
        );

        // Act: Сериализуем письмо в поток
        var jsonString = JsonSerializer.Serialize(originalLetter, JsonOptions);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
        var deserializedLetter = JsonSerializer.Deserialize<DefaultNetworkLetter>(stream);

        // Assert
        Assert.NotNull(deserializedLetter);
        Assert.Equal(targetNode, deserializedLetter.ReceiverNodeName);
        Assert.Equal(serializedContent, deserializedLetter.SerializedLetter);
        Assert.Equal(letterTypeName, deserializedLetter.LetterTypeName);

        // Дополнительно: проверим, что вложенные данные тоже восстанавливаются
        var restoredPayload = JsonSerializer.Deserialize<TestPayload>(deserializedLetter.SerializedLetter);
        Assert.NotNull(restoredPayload);
        Assert.Equal("Hello Network!", restoredPayload.Message);
    }

    /// <summary>
    /// Проверка: десериализация пустого потока вызывает исключение (поведение JsonSerializer)
    /// </summary>
    [Fact]
    public void NetworkLetterTests_002()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Act & Assert
        // Прямой вызов JsonSerializer.Deserialize бросает исключение на пустом потоке в новых версиях .NET
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NetworkLetter>(emptyStream));

        Assert.Contains("The input does not contain any JSON tokens", exception.Message);
    }

    /// <summary>
    /// Проверка: десериализация неверного JSON выбрасывает исключение
    /// </summary>
    [Fact]
    public void NetworkLetterTests_003()
    {
        // Arrange
        var invalidJson = "this is not json";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NetworkLetter>(stream));
    }

    /// <summary>
    /// Вспомогательный класс для тестирования полезной нагрузки
    /// </summary>
    public class TestPayload
    {
        public string Message { get; set; } = string.Empty;
    }
}
