using System.Globalization;

namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Плоское описание одного элемента модели — актора или вспомогательного объекта.
/// Единственный обязательный атрибут — Uid (или uid, Id, id).
/// Остальные атрибуты добавляются через <see cref="AddProperty"/> и извлекаются типизированными TryGet-методами.
/// </summary>
/// <remarks>
/// Flat description of a single model element — actor or helper object.
/// The only required attribute is Uid (or uid, Id, id).
/// Other attributes are added via AddProperty and retrieved via typed TryGet methods.
/// </remarks>
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
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства.</param>
    /// <remarks>
    /// Adds a string property.
    /// </remarks>
    public void AddProperty(string name, string value)
    {
        _properties[name] = value;
    }

    /// <summary>
    /// Проверяет наличие свойства.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <returns>true, если свойство существует; иначе false.</returns>
    /// <remarks>
    /// Checks whether a property exists.
    /// </remarks>
    public bool HasProperty(string name) => _properties.ContainsKey(name);

    /// <summary>
    /// Пытается получить строковое значение свойства.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует; иначе false.</returns>
    /// <remarks>
    /// Tries to get a string property value.
    /// </remarks>
    public bool TryGetString(string name, out string value)
        => _properties.TryGetValue(name, out value!);

    /// <summary>
    /// Пытается получить значение свойства как <see cref="int"/>.
    /// Поддерживает десятичную, шестнадцатеричную (0x), двоичную (0b) и восьмеричную (0o) запись.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as int.
    /// Supports decimal, hexadecimal (0x), binary (0b), and octal (0o) formats.
    /// </remarks>
    public bool TryGetInt32(string name, out int value)
    {
        value = 0;
        if (!_properties.TryGetValue(name, out string? str))
            return false;

        if (TryParseNumber(str, out value))
            return true;

        return int.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="long"/>.
    /// Поддерживает десятичную, шестнадцатеричную (0x), двоичную (0b) и восьмеричную (0o) запись.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as long.
    /// Supports decimal, hexadecimal (0x), binary (0b), and octal (0o) formats.
    /// </remarks>
    public bool TryGetInt64(string name, out long value)
    {
        value = 0;
        if (!_properties.TryGetValue(name, out string? str))
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
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as double.
    /// Supports decimal, hexadecimal (0x), binary (0b), and octal (0o) formats.
    /// Uses invariant culture (dot as decimal separator).
    /// </remarks>
    public bool TryGetDouble(string name, out double value)
    {
        value = 0.0;
        if (!_properties.TryGetValue(name, out string? str))
            return false;

        if (TryParseNumber(str, out value))
            return true;

        return double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="bool"/>.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as bool.
    /// </remarks>
    public bool TryGetBoolean(string name, out bool value)
    {
        value = false;
        return _properties.TryGetValue(name, out string? str) && bool.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как <see cref="Guid"/>.
    /// </summary>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as Guid.
    /// </remarks>
    public bool TryGetGuid(string name, out Guid value)
    {
        value = Guid.Empty;
        return _properties.TryGetValue(name, out string? str) && Guid.TryParse(str, out value);
    }

    /// <summary>
    /// Пытается получить значение свойства как перечисление.
    /// </summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    /// <param name="name">Имя свойства.</param>
    /// <param name="value">Значение свойства при успехе.</param>
    /// <returns>true, если свойство существует и может быть преобразовано; иначе false.</returns>
    /// <remarks>
    /// Tries to get a property value as an enum.
    /// </remarks>
    public bool TryGetEnum<T>(string name, out T value) where T : struct, Enum
    {
        value = default;
        return _properties.TryGetValue(name, out string? str) && Enum.TryParse(str, true, out value);
    }

    /// <summary>
    /// Пытается разобрать число с префиксом (0x, 0b, 0o) в int.
    /// </summary>
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

    /// <summary>
    /// Пытается разобрать число с префиксом (0x, 0b, 0o) в long.
    /// </summary>
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

    /// <summary>
    /// Пытается разобрать число с префиксом (0x, 0b, 0o) в double.
    /// </summary>
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
