namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Правило разбора XML-элемента. Определяет, какие атрибуты считать свойствами,
/// какие из них обязательны, и какие вложенные элементы являются группирующими.
/// Атрибуты <c>Uid</c> и <c>Source</c> обрабатываются компилятором автоматически.
/// </summary>
public class ElementRule
{
    /// <summary>
    /// Правило по умолчанию: все атрибуты считаются свойствами, обязательных и группирующих элементов нет.
    /// </summary>
    public static readonly ElementRule Default = new();

    private readonly HashSet<string> _properties = [];
    private readonly HashSet<string> _required = [];
    private readonly HashSet<string> _groupElements = [];

    /// <summary>
    /// Регистрирует атрибут как свойство элемента.
    /// </summary>
    public ElementRule WithProperty(string name)
    {
        _properties.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько атрибутов как свойства элемента.
    /// </summary>
    public ElementRule WithProperties(params string[] names)
    {
        foreach (var name in names)
            _properties.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует атрибут как обязательное свойство.
    /// Если свойство не было зарегистрировано через <see cref="WithProperty"/>,
    /// оно будет добавлено автоматически.
    /// </summary>
    public ElementRule WithRequired(string name)
    {
        _properties.Add(name);
        _required.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько атрибутов как обязательные свойства.
    /// </summary>
    public ElementRule WithRequired(params string[] names)
    {
        foreach (var name in names)
        {
            _properties.Add(name);
            _required.Add(name);
        }
        return this;
    }

    /// <summary>
    /// Регистрирует вложенный элемент как группирующий (маркер).
    /// Сам элемент в модель не попадает, его дети разбираются рекурсивно.
    /// </summary>
    public ElementRule WithGroupElement(string name)
    {
        _groupElements.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько вложенных элементов как группирующие.
    /// </summary>
    public ElementRule WithGroupElements(params string[] names)
    {
        foreach (var name in names)
            _groupElements.Add(name);
        return this;
    }

    /// <summary>
    /// Является ли атрибут свойством.
    /// Если свойства не указаны явно — все атрибуты считаются свойствами.
    /// </summary>
    public bool IsProperty(string name)
        => _properties.Count == 0 || _properties.Contains(name);

    /// <summary>
    /// Является ли свойство обязательным.
    /// </summary>
    public bool IsRequired(string name)
        => _required.Contains(name);

    /// <summary>
    /// Является ли вложенный элемент группирующим.
    /// </summary>
    public bool IsGroupElement(string name)
        => _groupElements.Contains(name);

    /// <summary>
    /// Возвращает список имён обязательных свойств.
    /// </summary>
    public IReadOnlyCollection<string> GetRequired()
        => _required;
}
