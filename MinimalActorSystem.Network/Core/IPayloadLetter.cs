namespace MinimalActorSystem.Network;

/// <summary>
/// Интерфейс, реализуемый письмами-носителями, которые могут быть отправлены через сеть.
/// Позволяет NetworkActor извлекать Payload (DTO) для сериализации и отправки.
/// </summary>
/// <remarks>
/// Interface implemented by carrier letters that can be sent over the network.
/// Allows NetworkActor to extract the Payload (DTO) for serialization and sending.
/// </remarks>
public interface IPayloadLetter
{
    /// <summary>
    /// Полезная нагрузка письма (DTO из сборки Topology).
    /// Содержит бизнес-данные, передаваемые между узлами.
    /// </summary>
    /// <remarks>
    /// The payload of the letter (DTO from the Topology assembly).
    /// Contains business data transferred between nodes.
    /// </remarks>
    object Payload { get; set; }

    /// <summary>
    /// Тип полезной нагрузки. Используется для маршрутизации и сериализации.
    /// </summary>
    /// <remarks>
    /// The type of the payload. Used for routing and serialization.
    /// </remarks>
    Type PayloadType { get; }
}
