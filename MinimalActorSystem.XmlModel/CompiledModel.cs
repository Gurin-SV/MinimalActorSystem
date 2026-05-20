using System.Linq;

namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Результат компиляции XML-описания модели в плоский набор элементов.
/// Связи между элементами устанавливаются прикладным кодом на основе атрибутов.
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
    public void Add(ElementConfig element)
    {
        _elements[element.Uid] = element;
    }

    /// <summary>
    /// Находит элемент по идентификатору.
    /// </summary>
    public ElementConfig? FindElement(Guid uid)
    {
        return _elements.TryGetValue(uid, out var element) ? element : null;
    }

    /// <summary>
    /// Находит все элементы указанного типа.
    /// </summary>
    public IReadOnlyList<ElementConfig> FindByType(string elementType)
    {
        return [.. _elements.Values.Where(e => e.ElementType == elementType)];
    }
}
