namespace MinimalActorSystem.Network;

/// <summary>
/// Отложенное сетевое сообщение.
/// </summary>
/// <remarks>
/// Pending network message.
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр отложенного сообщения.
/// </remarks>
public sealed class PendingMessage(Guid localMessageId, string destinationNode)
{
    /// <summary>
    /// Локальный идентификатор сообщения.
    /// </summary>
    public Guid LocalMessageId { get; } = localMessageId;

    /// <summary>
    /// Полезная нагрузка (DTO).
    /// </summary>
    public object Payload { get; set; } = null!;

    /// <summary>
    /// Тип полезной нагрузки.
    /// </summary>
    public Type PayloadType { get; set; } = null!;

    /// <summary>
    /// Тип оригинального письма.
    /// </summary>
    public Type LetterType { get; set; } = null!;

    /// <summary>
    /// Имя узла назначения.
    /// </summary>
    public string DestinationNode { get; set; } = destinationNode
        ?? throw new ArgumentNullException(nameof(destinationNode));

    /// <summary>
    /// Имя узла-отправителя.
    /// </summary>
    public string SourceNode { get; set; } = string.Empty;

    /// <summary>
    /// Время создания.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Время отправки.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Отправлено ли сообщение.
    /// </summary>
    public bool IsSent { get; set; } = false;

    /// <summary>
    /// Находится ли в очереди отложенных.
    /// </summary>
    public bool IsQueued { get; set; } = false;

    /// <summary>
    /// Количество попыток отправки.
    /// </summary>
    public int AttemptCount { get; set; } = 0;
}
