namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Правило разбора XML-элемента. Определяет, какие атрибуты считать свойствами
/// и какие из них обязательны для заполнения.
/// Атрибуты <c>Uid</c> и <c>Source</c> обрабатываются компилятором автоматически.
/// </summary>
public class ElementRule
{
    /// <summary>
    /// Правило по умолчанию: все атрибуты считаются свойствами, обязательных нет.
    /// </summary>
    public static readonly ElementRule Default = new();

    private readonly HashSet<string> _properties = [];
    private readonly HashSet<string> _required = [];

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
    /// Возвращает список имён обязательных свойств.
    /// </summary>
    public IReadOnlyCollection<string> GetRequired()
        => _required;
}
