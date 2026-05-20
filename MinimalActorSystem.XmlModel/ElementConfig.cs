using System.Globalization;

namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Плоское описание одного элемента модели — актора или вспомогательного объекта.
/// Единственный обязательный атрибут — Uid (или uid, Id, id).
/// Остальные атрибуты добавляются через <see cref="AddProperty"/> и извлекаются типизированными TryGet-методами.
/// </summary>
public class ElementConfig
{
    private readonly Dictionary<string, string> _properties = [];

    /// <summary>
    /// Уникальный идентификатор элемента.
    /// </summary>
    public Guid Uid { get; init; }

    /// <summary>
    /// Тип элемента.
    /// </summary>
    public string ElementType { get; init; } = string.Empty;

    /// <summary>
    /// Количество свойств элемента.
    /// </summary>
    public int PropertyCount => _properties.Count;

    /// <summary>
    /// Имена всех свойств элемента.
    /// </summary>
    public IReadOnlyCollection<string> PropertyNames => _properties.Keys;

    /// <summary>
    /// Добавляет строковое свойство.
    /// </summary>
    public void AddProperty(string name, string value)
    {
        _properties[name] = value;
    }

    /// <summary>
    /// Проверяет наличие свойства.
    /// </summary>
    public bool HasProperty(string name) => _properties.ContainsKey(name);

    /// <summary>
    /// Пытается получить строковое значение свойства.
    /// </summary>
    public bool TryGetString(string name, out string value)
        => _properties.TryGetValue(name, out value!);

    /// <summary>
    /// Пытается получить значение свойства как <see cref="int"/>.
    /// Поддерживает десятичную, шестнадцатеричную (0x), двоичную (0b) и восьмеричную (0o) запись.
    /// </summary>
    public bool TryGetInt32(string name, out int value)
    {
        value = 0;
        if (!_properties.TryGetValue(name, out var str))
            return false;

        if (TryParseNumber(str, out value))
            return true;

        return int.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="long"/>.
    /// Поддерживает десятичную, шестнадцатеричную (0x), двоичную (0b) и восьмеричную (0o) запись.
    /// </summary>
    public bool TryGetInt64(string name, out long value)
    {
        value = 0;
        if (!_properties.TryGetValue(name, out var str))
            return false;

        if (TryParseNumber(str, out value))
            return true;

        return long.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="double"/>.
    /// Поддерживает десятичную, шестнадцатеричную (0x), двоичную (0b) и восьмеричную (0o) запись.
    /// Всегда использует инвариантную культуру (точка как десятичный разделитель).
    /// </summary>
    public bool TryGetDouble(string name, out double value)
    {
        value = 0.0;
        if (!_properties.TryGetValue(name, out var str))
            return false;

        if (TryParseNumber(str, out value))
            return true;

        return double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="bool"/>.
    /// </summary>
    public bool TryGetBoolean(string name, out bool value)
    {
        value = false;
        return _properties.TryGetValue(name, out var str) && bool.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="Guid"/>.
    /// </summary>
    public bool TryGetGuid(string name, out Guid value)
    {
        value = Guid.Empty;
        return _properties.TryGetValue(name, out var str) && Guid.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как перечисление.
    /// </summary>
    public bool TryGetEnum<T>(string name, out T value) where T : struct, Enum
    {
        value = default;
        return _properties.TryGetValue(name, out var str) && Enum.TryParse(str, true, out value);
    }

    private static bool TryParseNumber(string str, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(str))
            return false;

        if (str.StartsWith("0x") || str.StartsWith("0X"))
            return int.TryParse(str[2..], NumberStyles.HexNumber, null, out value);

        if (str.StartsWith("0b") || str.StartsWith("0B"))
        {
            try { value = Convert.ToInt32(str[2..], 2); return true; }
            catch { return false; }
        }

        if (str.StartsWith("0o") || str.StartsWith("0O"))
        {
            try { value = Convert.ToInt32(str[2..], 8); return true; }
            catch { return false; }
        }

        return false;
    }

    private static bool TryParseNumber(string str, out long value)
    {
        value = 0;
        if (string.IsNullOrEmpty(str))
            return false;

        if (str.StartsWith("0x") || str.StartsWith("0X"))
            return long.TryParse(str[2..], NumberStyles.HexNumber, null, out value);

        if (str.StartsWith("0b") || str.StartsWith("0B"))
        {
            try { value = Convert.ToInt64(str[2..], 2); return true; }
            catch { return false; }
        }

        if (str.StartsWith("0o") || str.StartsWith("0O"))
        {
            try { value = Convert.ToInt64(str[2..], 8); return true; }
            catch { return false; }
        }

        return false;
    }

    private static bool TryParseNumber(string str, out double value)
    {
        value = 0.0;
        if (string.IsNullOrEmpty(str))
            return false;

        if (str.StartsWith("0x") || str.StartsWith("0X"))
        {
            try { value = Convert.ToInt64(str[2..], 16); return true; }
            catch { return false; }
        }

        if (str.StartsWith("0b") || str.StartsWith("0B"))
        {
            try { value = Convert.ToInt64(str[2..], 2); return true; }
            catch { return false; }
        }

        if (str.StartsWith("0o") || str.StartsWith("0O"))
        {
            try { value = Convert.ToInt64(str[2..], 8); return true; }
            catch { return false; }
        }

        return false;
    }
}
