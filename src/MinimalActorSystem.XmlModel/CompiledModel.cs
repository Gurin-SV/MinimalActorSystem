using System.Linq;

namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Результат компиляции XML-описания модели в плоский набор элементов.
/// Связи между элементами устанавливаются прикладным кодом на основе атрибутов.
/// </summary>
/// <remarks>
/// Result of compiling an XML model description into a flat set of elements.
/// Relationships between elements are established by application code based on attributes.
/// </remarks>
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
    /// <param name="element">Элемент для добавления.</param>
    /// <remarks>
    /// Adds an element to the model. If an element with the same Uid already exists, it is replaced.
    /// </remarks>
    public void Add(ElementConfig element)
    {
        _elements[element.Uid] = element;
    }

    /// <summary>
    /// Находит элемент по идентификатору.
    /// </summary>
    /// <param name="uid">Идентификатор элемента.</param>
    /// <returns>Найденный элемент или null, если элемент не найден.</returns>
    /// <remarks>
    /// Finds an element by its unique identifier. Returns null if not found.
    /// </remarks>
    public ElementConfig? FindElement(Guid uid)
    {
        return _elements.TryGetValue(uid, out ElementConfig? element) ? element : null;
    }

    /// <summary>
    /// Находит все элементы указанного типа.
    /// </summary>
    /// <param name="elementType">Тип элемента (значение атрибута или тега).</param>
    /// <returns>Список элементов указанного типа.</returns>
    /// <remarks>
    /// Finds all elements of the specified type.
    /// </remarks>
    public IReadOnlyList<ElementConfig> FindByType(string elementType)
    {
        return [.. _elements.Values.Where(e => e.ElementType == elementType)];
    }
}
