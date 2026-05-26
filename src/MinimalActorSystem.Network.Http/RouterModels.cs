using System.Text.Json.Serialization;

namespace MinimalActorSystem.Network.Http;

/// <summary>
/// Модель запроса для регистрации узла.
/// </summary>
/// <remarks>
/// Request model for node registration.
/// </remarks>
public sealed class RegisterNodeRequest
{
    /// <summary>
    /// Имя узла.
    /// </summary>
    /// <remarks>
    /// Name of the node.
    /// </remarks>
    [JsonPropertyName("nodeName")]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Сетевой адрес узла (URL или IP:port).
    /// </summary>
    /// <remarks>
    /// Network address of the node (URL or IP:port).
    /// </remarks>
    [JsonPropertyName("nodeAddress")]
    public string NodeAddress { get; set; } = string.Empty;
}

/// <summary>
/// Модель ответа на регистрацию узла.
/// </summary>
/// <remarks>
/// Response model for node registration.
/// </remarks>
public sealed class RegisterNodeResponse
{
    /// <summary>
    /// Успешность операции.
    /// </summary>
    /// <remarks>
    /// Operation success flag.
    /// </remarks>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение (описание ошибки или успеха).
    /// </summary>
    /// <remarks>
    /// Message (error description or success).
    /// </remarks>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Модель запроса для разрешения имени узла.
/// </summary>
/// <remarks>
/// Request model for node resolution.
/// </remarks>
public sealed class ResolveNodeRequest
{
    /// <summary>
    /// Имя узла.
    /// </summary>
    /// <remarks>
    /// Name of the node.
    /// </remarks>
    [JsonPropertyName("nodeName")]
    public string NodeName { get; set; } = string.Empty;
}

/// <summary>
/// Модель ответа на разрешение имени узла.
/// </summary>
/// <remarks>
/// Response model for node resolution.
/// </remarks>
public sealed class ResolveNodeResponse
{
    /// <summary>
    /// Сетевой адрес узла.
    /// </summary>
    /// <remarks>
    /// Network address of the node.
    /// </remarks>
    [JsonPropertyName("nodeAddress")]
    public string? NodeAddress { get; set; }

    /// <summary>
    /// Признак того, что узел найден.
    /// </summary>
    /// <remarks>
    /// Indicates whether the node was found.
    /// </remarks>
    [JsonPropertyName("found")]
    public bool Found { get; set; }
}

/// <summary>
/// Модель ответа на запрос всех узлов.
/// </summary>
/// <remarks>
/// Response model for getting all nodes.
/// </remarks>
public sealed class GetAllNodesResponse
{
    /// <summary>
    /// Словарь соответствия имя узла -> сетевой адрес.
    /// </summary>
    /// <remarks>
    /// Dictionary mapping node name to network address.
    /// </remarks>
    [JsonPropertyName("nodes")]
    public Dictionary<string, string> Nodes { get; set; } = [];
}

/// <summary>
/// Модель ответа на проверку доступности узла.
/// </summary>
/// <remarks>
/// Response model for node health check.
/// </remarks>
public sealed class HealthCheckResponse
{
    /// <summary>
    /// Статус узла (например, "healthy", "unhealthy").
    /// </summary>
    /// <remarks>
    /// Node status (e.g., "healthy", "unhealthy").
    /// </remarks>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Имя узла.
    /// </summary>
    /// <remarks>
    /// Name of the node.
    /// </remarks>
    [JsonPropertyName("nodeName")]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Временная метка проверки.
    /// </summary>
    /// <remarks>
    /// Timestamp of the health check.
    /// </remarks>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Модель запроса на отправку сетевого сообщения между узлами.
/// </summary>
/// <remarks>
/// Request model for sending network message between nodes.
/// </remarks>
public sealed class SendMessageRequest
{
    /// <summary>
    /// Имя узла-отправителя.
    /// </summary>
    /// <remarks>
    /// Name of the source node.
    /// </remarks>
    [JsonPropertyName("sourceNodeName")]
    public string SourceNodeName { get; set; } = string.Empty;

    /// <summary>
    /// Сериализованное сообщение.
    /// </summary>
    /// <remarks>
    /// Serialized message.
    /// </remarks>
    [JsonPropertyName("serializedMessage")]
    public string SerializedMessage { get; set; } = string.Empty;
}

/// <summary>
/// Модель ответа на отправку сообщения.
/// </summary>
/// <remarks>
/// Response model for sending message.
/// </remarks>
public sealed class SendMessageResponse
{
    /// <summary>
    /// Успешность операции.
    /// </summary>
    /// <remarks>
    /// Operation success flag.
    /// </remarks>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение (описание ошибки или успеха).
    /// </summary>
    /// <remarks>
    /// Message (error description or success).
    /// </remarks>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
