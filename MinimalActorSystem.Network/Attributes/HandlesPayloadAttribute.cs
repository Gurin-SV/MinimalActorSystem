namespace MinimalActorSystem.Network;

/// <summary>
/// Указывает, что метод в NetworkActor обрабатывает Payload указанного типа.
/// Метод должен возвращать Guid идентификатор локального актора-обработчика.
/// </summary>
/// <remarks>
/// Indicates that a method in NetworkActor handles Payload of the specified type.
/// The method must return the Guid of the local actor-handler.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class HandlesPayloadAttribute : Attribute
{
    /// <summary>
    /// Тип Payload, который обрабатывает отмеченный метод.
    /// </summary>
    /// <remarks>
    /// The type of Payload that the marked method handles.
    /// </remarks>
    public Type PayloadType { get; }

    /// <summary>
    /// Инициализирует новый экземпляр атрибута с указанным типом Payload.
    /// </summary>
    /// <param name="payloadType">Тип Payload. Не может быть null.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если payloadType равен null.</exception>
    /// <remarks>
    /// Initializes a new instance of the attribute with the specified Payload type.
    /// </remarks>
    public HandlesPayloadAttribute(Type payloadType)
    {
        if (payloadType == null)
            throw new ArgumentNullException(nameof(payloadType));

        PayloadType = payloadType;
    }
}
