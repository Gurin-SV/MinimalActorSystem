using System.IO;
using System.Linq;
using System.Xml;

namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Конфигурируемый XML-компилятор модели.
/// Разбирает XML в плоский набор <see cref="ElementConfig"/>.
/// Обязательный атрибут — Uid (или uid, Id, id). Элементы без Uid пропускаются с предупреждением.
/// Группирующие элементы пропускаются, их дети разбираются рекурсивно.
/// Вложенные элементы без атрибутов, содержащие только текст, сохраняются как свойства родителя.
/// </summary>
/// <remarks>
/// Configurable XML model compiler.
/// Parses XML into a flat set of ElementConfig objects.
/// Uid attribute is required. Elements without Uid are skipped with a warning.
/// Grouping elements are skipped; their children are processed recursively.
/// Nested elements with no attributes containing only text are stored as parent properties.
/// </remarks>
/// <param name="uidAttributeName">Имя атрибута для идентификатора. По умолчанию "Uid".</param>
public class XmlModelCompiler(string uidAttributeName = "Uid")
{
    private readonly Dictionary<string, ElementRule> _rules = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];
    private readonly string _uidAttributeName = uidAttributeName;

    /// <summary>
    /// Предупреждения, собранные в процессе компиляции.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Ошибки, собранные в процессе компиляции.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Добавляет правило разбора для элементов с указанным именем.
    /// </summary>
    /// <param name="elementName">Имя XML-элемента.</param>
    /// <param name="rule">Правило разбора. Если не указано, используется <see cref="ElementRule.Default"/>.</param>
    /// <returns>Текущий компилятор для цепочки вызовов.</returns>
    /// <remarks>
    /// Adds a parsing rule for elements with the specified name.
    /// </remarks>
    public XmlModelCompiler AddRule(string elementName, ElementRule? rule = null)
    {
        _rules[elementName] = rule ?? ElementRule.Default;
        return this;
    }

    /// <summary>
    /// Компилирует XML-строку в модель.
    /// </summary>
    /// <param name="xml">XML-описание модели.</param>
    /// <returns>Скомпилированная модель.</returns>
    /// <remarks>
    /// Compiles an XML string into a model.
    /// </remarks>
    public CompiledModel Compile(string xml)
    {
        _warnings.Clear();
        _errors.Clear();

        using StringReader stringReader = new(xml);
        using XmlReader xmlReader = XmlReader.Create(stringReader);
        CompiledModel model = new();

        xmlReader.MoveToContent();

        if (xmlReader.NodeType == XmlNodeType.Element)
        {
            xmlReader.ReadStartElement();
        }

        while (xmlReader.NodeType != XmlNodeType.EndElement && xmlReader.NodeType != XmlNodeType.None)
        {
            if (xmlReader.NodeType == XmlNodeType.Element)
            {
                CompileElement(xmlReader, model);
            }
            else
            {
                xmlReader.Skip();
            }
        }

        return model;
    }

    /// <summary>
    /// Компилирует один XML-элемент и добавляет его в модель.
    /// </summary>
    private void CompileElement(XmlReader reader, CompiledModel model)
    {
        string elementName = reader.Name;
        ElementRule rule = _rules.TryGetValue(elementName, out ElementRule? r) ? r : ElementRule.Default;

        // Если это группирующий элемент — обрабатываем его детей
        if (rule.IsGroupElement(elementName))
        {
            CompileGroupElement(reader, model);
            return;
        }

        Guid? uid = ReadUid(reader);
        if (uid == null)
        {
            _warnings.Add($"Element '{elementName}': missing Uid attribute. Element skipped.");
            SkipElement(reader);
            return;
        }

        ElementConfig element = new()
        {
            Uid = uid.Value,
            ElementType = elementName
        };

        while (reader.MoveToNextAttribute())
        {
            if (rule.IsProperty(reader.Name))
            {
                element.AddProperty(reader.Name, reader.Value);
            }
        }
        reader.MoveToElement();

        List<string> missingRequired = [.. rule.GetRequired().Where(req => !element.HasProperty(req))];

        if (missingRequired.Count > 0)
        {
            _warnings.Add(
                $"Element '{elementName}' (Uid={uid}): missing required properties: {string.Join(", ", missingRequired)}. Element skipped.");
            SkipElement(reader);
            return;
        }

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            model.Add(element);
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.SignificantWhitespace:
                    string text = reader.ReadContentAsString().Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        element.AddProperty(elementName, text);
                    }
                    break;

                case XmlNodeType.Element:
                    if (rule.IsGroupElement(reader.Name))
                    {
                        CompileGroupElement(reader, model);
                    }
                    else if (IsTextProperty(reader))
                    {
                        (string? Name, string? Text) = ReadTextProperty(reader);
                        element.AddProperty(Name, Text);
                    }
                    else
                    {
                        CompileElement(reader, model);
                    }
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        reader.ReadEndElement();
        model.Add(element);
    }

    /// <summary>
    /// Обрабатывает группирующий элемент: пропускает его и рекурсивно обрабатывает детей.
    /// </summary>
    private void CompileGroupElement(XmlReader reader, CompiledModel model)
    {
        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                CompileElement(reader, model);
            }
            else
            {
                reader.Skip();
            }
        }

        reader.ReadEndElement();
    }

    /// <summary>
    /// Проверяет, является ли элемент текстовым свойством (не имеет атрибутов и не пустой).
    /// </summary>
    private static bool IsTextProperty(XmlReader reader)
    {
        return reader.AttributeCount == 0 && !reader.IsEmptyElement;
    }

    /// <summary>
    /// Считывает текстовое свойство: имя элемента и его содержимое.
    /// </summary>
    private static (string Name, string Text) ReadTextProperty(XmlReader reader)
    {
        string name = reader.Name;
        reader.ReadStartElement();
        string text = reader.ReadContentAsString().Trim();
        reader.ReadEndElement();
        return (name, text);
    }

    /// <summary>
    /// Считывает Uid из атрибута. Возвращает null, если атрибут отсутствует или не является валидным Guid.
    /// </summary>
    private Guid? ReadUid(XmlReader reader)
    {
        string uidAttr = reader.GetAttribute(_uidAttributeName);
        return !string.IsNullOrEmpty(uidAttr) && Guid.TryParse(uidAttr, out Guid uid) ? uid : null;
    }

    /// <summary>
    /// Пропускает элемент и всё его содержимое.
    /// </summary>
    private static void SkipElement(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
        }
        else
        {
            reader.ReadStartElement();
            reader.Skip();
            reader.ReadEndElement();
        }
    }
}
