using System.Linq;

namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Результат компиляции XML-описания модели в плоский граф элементов.
/// Собирается компилятором через <see cref="Add"/>, после чего отдаётся как готовый неизменяемый результат.
/// Каждый элемент уникален по Uid. Связи источник-потребитель выводятся из <see cref="ElementConfig.SourceUid"/>.
/// </summary>
public class CompiledModel
{
    private readonly Dictionary<Guid, ElementConfig> _elements = [];

    /// <summary>
    /// Идентификаторы всех элементов модели.
    /// </summary>
    public IReadOnlyCollection<Guid> Uids => _elements.Keys;

    /// <summary>
    /// Количество элементов в модели.
    /// </summary>
    public int Count => _elements.Count;

    /// <summary>
    /// Добавляет элемент в модель. Если элемент с таким Uid уже существует, он заменяется.
    /// </summary>
    /// <param name="element">Конфигурация элемента.</param>
    public void Add(ElementConfig element)
    {
        _elements[element.Uid] = element;
    }

    /// <summary>
    /// Находит элемент по идентификатору.
    /// </summary>
    /// <param name="uid">Идентификатор элемента.</param>
    /// <returns>Найденный элемент или <c>null</c>.</returns>
    public ElementConfig? FindElement(Guid uid)
    {
        return _elements.TryGetValue(uid, out var element) ? element : null;
    }

    /// <summary>
    /// Находит все элементы, для которых указанный Uid является источником.
    /// Соответствует направлению потока данных: источник → потребители.
    /// </summary>
    /// <param name="sourceUid">Идентификатор элемента-источника.</param>
    /// <returns>Список элементов-потребителей (может быть пустым).</returns>
    public IReadOnlyList<ElementConfig> GetConsumersOf(Guid sourceUid)
    {
        return [.. _elements.Values.Where(e => e.SourceUid == sourceUid)];
    }
}
