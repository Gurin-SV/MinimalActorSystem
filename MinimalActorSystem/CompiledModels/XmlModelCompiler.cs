using System.IO;
using System.Linq;
using System.Xml;

namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Конфигурируемый XML-компилятор модели.
/// Разбирает XML в плоский набор <see cref="ElementConfig"/>.
/// Обязательный атрибут — Uid (или uid, Id, id). Элементы без Uid пропускаются с предупреждением.
/// Группирующие элементы пропускаются, их дети разбираются рекурсивно.
/// Вложенные элементы без атрибутов, содержащие только текст, сохраняются как свойства родителя.
/// </summary>
/// <remarks>
/// Создаёт компилятор с именем атрибута идентификатора по умолчанию "Uid".
/// </remarks>
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
    public CompiledModel Compile(string xml)
    {
        _warnings.Clear();
        _errors.Clear();

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader);
        var model = new CompiledModel();

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

    private void CompileElement(XmlReader reader, CompiledModel model)
    {
        var elementName = reader.Name;
        var rule = _rules.TryGetValue(elementName, out var r) ? r : ElementRule.Default;

        // Если это группирующий элемент — обрабатываем его детей
        if (rule.IsGroupElement(elementName))
        {
            CompileGroupElement(reader, model);
            return;
        }

        var uid = ReadUid(reader);
        if (uid == null)
        {
            _warnings.Add($"Element '{elementName}': missing Uid attribute. Element skipped.");
            SkipElement(reader);
            return;
        }

        var element = new ElementConfig
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

        var missingRequired = rule.GetRequired()
            .Where(req => !element.HasProperty(req))
            .ToList();

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
                    var text = reader.ReadContentAsString().Trim();
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
                        var prop = ReadTextProperty(reader);
                        element.AddProperty(prop.Name, prop.Text);
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

    private static bool IsTextProperty(XmlReader reader)
    {
        return reader.AttributeCount == 0 && !reader.IsEmptyElement;
    }

    private static (string Name, string Text) ReadTextProperty(XmlReader reader)
    {
        var name = reader.Name;
        reader.ReadStartElement();
        var text = reader.ReadContentAsString().Trim();
        reader.ReadEndElement();
        return (name, text);
    }

    private Guid? ReadUid(XmlReader reader)
    {
        var uidAttr = reader.GetAttribute(_uidAttributeName);
        return !string.IsNullOrEmpty(uidAttr) && Guid.TryParse(uidAttr, out var uid) ? uid : null;
    }

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
