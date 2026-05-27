namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Правило разбора XML-элемента. Определяет, какие атрибуты считать свойствами,
/// какие из них обязательны, и какие вложенные элементы являются группирующими.
/// Атрибуты <c>Uid</c> и <c>Source</c> обрабатываются компилятором автоматически.
/// </summary>
/// <remarks>
/// Rule for parsing an XML element. Determines which attributes are properties,
/// which are required, and which nested elements are grouping elements.
/// Uid and Source attributes are handled automatically by the compiler.
/// </remarks>
public class ElementRule
{
    /// <summary>
    /// Правило по умолчанию: все атрибуты считаются свойствами, обязательных и группирующих элементов нет.
    /// </summary>
    /// <remarks>
    /// Default rule: all attributes are properties, no required or grouping elements.
    /// </remarks>
    public static readonly ElementRule Default = new();

    private readonly HashSet<string> _properties = [];
    private readonly HashSet<string> _required = [];
    private readonly HashSet<string> _groupElements = [];

    /// <summary>
    /// Регистрирует атрибут как свойство элемента.
    /// </summary>
    /// <param name="name">Имя атрибута.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers an attribute as an element property.
    /// </remarks>
    public ElementRule WithProperty(string name)
    {
        _properties.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько атрибутов как свойства элемента.
    /// </summary>
    /// <param name="names">Имена атрибутов.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers multiple attributes as element properties.
    /// </remarks>
    public ElementRule WithProperties(params string[] names)
    {
        foreach (string name in names)
            _properties.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует атрибут как обязательное свойство.
    /// Если свойство не было зарегистрировано через <see cref="WithProperty"/>,
    /// оно будет добавлено автоматически.
    /// </summary>
    /// <param name="name">Имя атрибута.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers an attribute as a required property.
    /// If the property wasn't registered via WithProperty, it is added automatically.
    /// </remarks>
    public ElementRule WithRequired(string name)
    {
        _properties.Add(name);
        _required.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько атрибутов как обязательные свойства.
    /// </summary>
    /// <param name="names">Имена атрибутов.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers multiple attributes as required properties.
    /// </remarks>
    public ElementRule WithRequired(params string[] names)
    {
        foreach (string name in names)
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
    /// <param name="name">Имя вложенного элемента.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers a nested element as a grouping element (marker).
    /// The element itself does not become part of the model; its children are processed recursively.
    /// </remarks>
    public ElementRule WithGroupElement(string name)
    {
        _groupElements.Add(name);
        return this;
    }

    /// <summary>
    /// Регистрирует несколько вложенных элементов как группирующие.
    /// </summary>
    /// <param name="names">Имена вложенных элементов.</param>
    /// <returns>Текущее правило для цепочки вызовов.</returns>
    /// <remarks>
    /// Registers multiple nested elements as grouping elements.
    /// </remarks>
    public ElementRule WithGroupElements(params string[] names)
    {
        foreach (string name in names)
            _groupElements.Add(name);
        return this;
    }

    /// <summary>
    /// Является ли атрибут свойством.
    /// Если свойства не указаны явно — все атрибуты считаются свойствами.
    /// </summary>
    /// <param name="name">Имя атрибута.</param>
    /// <returns>true, если атрибут является свойством; иначе false.</returns>
    /// <remarks>
    /// Checks whether an attribute is a property.
    /// If no properties are explicitly defined, all attributes are considered properties.
    /// </remarks>
    public bool IsProperty(string name)
        => _properties.Count == 0 || _properties.Contains(name);

    /// <summary>
    /// Является ли свойство обязательным.
    /// </summary>
    /// <param name="name">Имя атрибута.</param>
    /// <returns>true, если свойство обязательно; иначе false.</returns>
    /// <remarks>
    /// Checks whether a property is required.
    /// </remarks>
    public bool IsRequired(string name)
        => _required.Contains(name);

    /// <summary>
    /// Является ли вложенный элемент группирующим.
    /// </summary>
    /// <param name="name">Имя вложенного элемента.</param>
    /// <returns>true, если элемент является группирующим; иначе false.</returns>
    /// <remarks>
    /// Checks whether a nested element is a grouping element.
    /// </remarks>
    public bool IsGroupElement(string name)
        => _groupElements.Contains(name);

    /// <summary>
    /// Возвращает список имён обязательных свойств.
    /// </summary>
    /// <returns>Список обязательных свойств.</returns>
    /// <remarks>
    /// Returns the list of required property names.
    /// </remarks>
    public IReadOnlyCollection<string> GetRequired()
        => _required;
}
