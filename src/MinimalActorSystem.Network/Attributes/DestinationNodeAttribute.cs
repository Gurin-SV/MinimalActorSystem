namespace MinimalActorSystem.Network;

/// <summary>
/// Указывает, на какие узлы может быть отправлен Payload данного типа.
/// Может быть использован несколько раз для указания нескольких узлов назначения.
/// </summary>
/// <remarks>
/// Specifies which nodes a Payload of this type can be sent to.
/// Can be used multiple times to specify multiple destination nodes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DestinationNodeAttribute : Attribute
{
    /// <summary>
    /// Символическое имя узла назначения.
    /// </summary>
    /// <remarks>
    /// Symbolic name of the destination node.
    /// </remarks>
    public string NodeName { get; }

    /// <summary>
    /// Инициализирует новый экземпляр атрибута с указанным именем узла.
    /// </summary>
    /// <param name="nodeName">Символическое имя узла назначения. Не может быть пустым или состоять из пробелов.</param>
    /// <exception cref="ArgumentException">Выбрасывается, если nodeName пустой или состоит из пробелов.</exception>
    /// <remarks>
    /// Initializes a new instance of the attribute with the specified node name.
    /// </remarks>
    public DestinationNodeAttribute(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        NodeName = nodeName;
    }
}
