namespace MinimalActorSystem.Network;

/// <summary>
/// Базовый класс сетевого письма. Содержит информацию о целевом узле и сериализуемое вложенное письмо.
/// Наследники должны быть <c>sealed</c> и содержать конструктор по умолчанию для десериализации.
/// </summary>
/// <remarks>
/// Base class for network letters. Contains target node information and a serializable nested letter.
/// Derived classes must be sealed and have a parameterless constructor for deserialization.
/// </remarks>
public abstract class NetworkLetter
{
    /// <summary>
    /// Символическое имя узла-получателя.
    /// </summary>
    /// <remarks>
    /// Symbolic name of the target node.
    /// </remarks>
    public string ReceiverNodeName { get; init; } = string.Empty;

    /// <summary>
    /// Сериализованное содержимое оригинального письма в формате JSON.
    /// </summary>
    /// <remarks>
    /// Serialized content of the original letter in JSON format.
    /// </remarks>
    public string SerializedLetter { get; init; } = string.Empty;

    /// <summary>
    /// Тип оригинального письма (полное имя типа .NET). Используется при десериализации.
    /// </summary>
    /// <remarks>
    /// Type of the original letter (full .NET type name). Used during deserialization.
    /// </remarks>
    public string LetterTypeName { get; init; } = string.Empty;
}
