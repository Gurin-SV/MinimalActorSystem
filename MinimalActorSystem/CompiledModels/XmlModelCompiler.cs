using System.IO;
using System.Xml;

namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Конфигурируемый XML-компилятор модели.
/// Разбирает плоский XML в граф <see cref="CompiledModel"/>.
/// Связи источник-потребитель задаются явным атрибутом <c>Source</c> на элементе-потребителе.
/// Правила разбора свойств задаются через <see cref="AddRule"/>.
/// </summary>
public class XmlModelCompiler
{
    private readonly Dictionary<string, ElementRule> _rules = [];

    /// <summary>
    /// Добавляет правило разбора для элементов с указанным именем.
    /// </summary>
    /// <param name="elementName">Имя XML-элемента (например "AggregatedAnalogValue").</param>
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
    /// <returns>Скомпилированная модель с плоским графом элементов.</returns>
    /// <exception cref="InvalidOperationException">Отсутствует обязательное свойство.</exception>
    public CompiledModel Compile(string xml)
    {
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

    private ElementConfig CompileElement(XmlReader reader, CompiledModel model)
    {
        var elementName = reader.Name;
        var rule = _rules.TryGetValue(elementName, out var r) ? r : ElementRule.Default;

        var uid = ReadUid(reader);
        var element = new ElementConfig
        {
            Uid = uid,
            ElementType = elementName,
            SourceUid = ReadSourceUid(reader)
        };

        while (reader.MoveToNextAttribute())
        {
            if (rule.IsProperty(reader.Name))
            {
                element.Properties[reader.Name] = reader.Value;
            }
        }
        reader.MoveToElement();

        // Проверяем обязательные свойства
        foreach (var required in rule.GetRequired())
        {
            if (!element.Properties.ContainsKey(required))
            {
                throw new InvalidOperationException(
                    $"Element '{elementName}' (Uid={uid}): required property '{required}' is missing.");
            }
        }

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            model.Add(element);
            return element;
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
                        element.Properties[elementName] = text;
                    }
                    break;

                case XmlNodeType.Element:
                    // Вложенные элементы разбираются рекурсивно как отдельные элементы
                    CompileElement(reader, model);
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        reader.ReadEndElement();
        model.Add(element);
        return element;
    }

    private static Guid ReadUid(XmlReader reader)
    {
        var uidAttr = reader.GetAttribute("Uid");
        return !string.IsNullOrEmpty(uidAttr) && Guid.TryParse(uidAttr, out var uid)
            ? uid
            : Guid.NewGuid();
    }

    private static Guid? ReadSourceUid(XmlReader reader)
    {
        var sourceAttr = reader.GetAttribute("Source");
        return !string.IsNullOrEmpty(sourceAttr) && Guid.TryParse(sourceAttr, out var sourceUid)
            ? sourceUid
            : null;
    }
}
