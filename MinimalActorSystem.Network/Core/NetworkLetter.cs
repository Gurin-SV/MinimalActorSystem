using System.Text.Json.Serialization;

namespace MinimalActorSystem.Network;

/// <summary>
/// Сетевое сообщение - сериализуемый контейнер для передачи Payload между узлами.
/// Содержит информацию о целевом узле, узле-отправителе и полезную нагрузку.
/// </summary>
/// <remarks>
/// Network message - serializable container for transferring Payload between nodes.
/// Contains destination node, source node and payload information.
/// </remarks>
public sealed class NetworkLetter(
    string destinationNodeName,
    string sourceNodeName,
    string payloadJson,
    string payloadTypeName,
    string letterTypeName,
    Guid localLetterId)
{
    /// <summary>
    /// Символическое имя узла-получателя.
    /// </summary>
    /// <remarks>
    /// Symbolic name of the destination node.
    /// </remarks>
    public string DestinationNodeName { get; set; } = destinationNodeName;

    /// <summary>
    /// Символическое имя узла-отправителя.
    /// </summary>
    /// <remarks>
    /// Symbolic name of the source node.
    /// </remarks>
    public string SourceNodeName { get; set; } = sourceNodeName;

    /// <summary>
    /// Полезная нагрузка в формате JSON строки.
    /// </summary>
    /// <remarks>
    /// Payload as JSON string.
    /// </remarks>
    public string PayloadJson { get; set; } = payloadJson;

    /// <summary>
    /// AssemblyQualifiedName типа полезной нагрузки.
    /// </summary>
    /// <remarks>
    /// AssemblyQualifiedName of the payload type.
    /// </remarks>
    public string PayloadTypeName { get; set; } = payloadTypeName;

    /// <summary>
    /// AssemblyQualifiedName типа оригинального письма.
    /// </summary>
    /// <remarks>
    /// AssemblyQualifiedName of the original letter type.
    /// </remarks>
    public string LetterTypeName { get; set; } = letterTypeName;

    /// <summary>
    /// Локальный идентификатор письма (не передаётся по сети).
    /// Используется для отмены отправки при таймауте.
    /// </summary>
    /// <remarks>
    /// Local letter identifier (not sent over network).
    /// Used to cancel sending on timeout.
    /// </remarks>
    public Guid LocalLetterId { get; set; } = localLetterId;

    /// <summary>
    /// Тип полезной нагрузки (ленивое свойство, восстанавливает тип из строки).
    /// Не сериализуется.
    /// </summary>
    /// <remarks>
    /// Payload type (lazy property, restores type from string).
    /// Not serialized.
    /// </remarks>
    [JsonIgnore]
    public Type PayloadType => Type.GetType(PayloadTypeName, false) ?? typeof(object);

    /// <summary>
    /// Тип оригинального письма (ленивое свойство, восстанавливает тип из строки).
    /// Не сериализуется.
    /// </summary>
    /// <remarks>
    /// Letter type (lazy property, restores type from string).
    /// Not serialized.
    /// </remarks>
    [JsonIgnore]
    public Type LetterType => Type.GetType(LetterTypeName, false) ?? typeof(Letter);

    /// <summary>
    /// Конструктор без параметров для десериализации.
    /// </summary>
    /// <remarks>
    /// Parameterless constructor for deserialization.
    /// </remarks>
    public NetworkLetter() : this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Guid.Empty)
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Получает типизированный Payload.
    /// </summary>
    /// <typeparam name="T">Тип, в который нужно десериализовать Payload.</typeparam>
    /// <returns>Десериализованный объект типа T.</returns>
    /// <remarks>
    /// Gets typed Payload.
    /// </remarks>
    public T GetPayload<T>()
    {
        return JsonSerializer.Deserialize<T>(PayloadJson, JsonOptions)!;
    }

    /// <summary>
    /// Создаёт NetworkLetter из Payload с явным указанием типа.
    /// </summary>
    /// <param name="destinationNodeName">Имя узла-получателя.</param>
    /// <param name="sourceNodeName">Имя узла-отправителя.</param>
    /// <param name="payload">Полезная нагрузка.</param>
    /// <param name="payloadType">Тип полезной нагрузки.</param>
    /// <param name="letterType">Тип оригинального письма.</param>
    /// <param name="localLetterId">Локальный идентификатор письма.</param>
    /// <returns>Созданный NetworkLetter.</returns>
    /// <remarks>
    /// Creates NetworkLetter from Payload with explicit type specification.
    /// </remarks>
    public static NetworkLetter Create(
        string destinationNodeName,
        string sourceNodeName,
        object payload,
        Type payloadType,
        Type letterType,
        Guid localLetterId)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        return new NetworkLetter(
            destinationNodeName,
            sourceNodeName,
            payloadJson,
            payloadType.AssemblyQualifiedName!,
            letterType.AssemblyQualifiedName!,
            localLetterId);
    }
}
