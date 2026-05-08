namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Плоское описание одного элемента модели — актора или вспомогательного объекта.
/// Не содержит вложенности: иерархия из XML разворачивается компилятором в плоский список.
/// Связи между элементами задаются через <see cref="Sources"/>.
/// </summary>
public class ElementConfig
{
    /// <summary>
    /// Уникальный идентификатор элемента.
    /// Может быть задан в XML явно или сгенерирован компилятором.
    /// </summary>
    public Guid Uid { get; init; }

    /// <summary>
    /// Тип элемента. Определяет, какой класс актора или объекта будет создан фабрикой.
    /// Например: "AggregatedAnalogValue", "AnalogValue", "HISPartition".
    /// </summary>
    public string ElementType { get; init; } = string.Empty;

    /// <summary>
    /// Атрибуты элемента, полученные из XML.
    /// Ключ — имя атрибута, значение — строковое представление.
    /// Например: { "MethodType": "Max", "ScheduleType": "Month" }.
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = [];

    /// <summary>
    /// Идентификатор элемента-источника. Если элемент не имеет источника — <c>null</c>.
    /// Заполняется компилятором на основе вложенности в XML.
    /// Один источник может быть указан у многих потребителей.
    /// </summary>
    public Guid? SourceUid { get; init; }
}
