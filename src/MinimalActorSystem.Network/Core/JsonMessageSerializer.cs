namespace MinimalActorSystem.Network;

/// <summary>
/// Сериализатор сообщений в формате JSON.
/// Формат: "AssemblyQualifiedTypeName\0{...json...}"
/// </summary>
/// <remarks>
/// JSON message serializer.
/// Format: "AssemblyQualifiedTypeName\0{...json...}"
/// </remarks>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <inheritdoc/>
    public string Serialize(NetworkLetter letter)
    {
        if (letter == null)
            throw new ArgumentNullException(nameof(letter));

        var typeName = typeof(NetworkLetter).AssemblyQualifiedName;
        var json = JsonSerializer.Serialize(letter, JsonOptions);
        return $"{typeName}\0{json}";
    }

    /// <inheritdoc/>
    public NetworkLetter? Deserialize(string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        var separatorIndex = data.IndexOf('\0');
        if (separatorIndex == -1)
            return null;

        var typeName = data[..separatorIndex];
        var json = data[(separatorIndex + 1)..];

        var type = Type.GetType(typeName);
        if (type == null || type != typeof(NetworkLetter))
            return null;

        try
        {
            return JsonSerializer.Deserialize<NetworkLetter>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
