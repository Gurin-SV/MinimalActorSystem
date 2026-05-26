using System.Linq;
using System.Reflection;

namespace MinimalActorSystem.Network;

/// <summary>
/// Загрузчик топологии из сборки.
/// Предоставляет унифицированный доступ к метаданным топологии:
/// - список адресов роутеров
/// - список узлов
/// - DTO с атрибутами DestinationNode
/// </summary>
/// <remarks>
/// Topology loader from assembly.
/// Provides unified access to topology metadata:
/// - router addresses list
/// - node names list
/// - DTOs with DestinationNode attributes
/// </remarks>
public sealed class TopologyLoader
{
    private readonly Assembly _assembly;
    private readonly Lazy<IReadOnlyList<string>> _routerAddresses;
    private readonly Lazy<IReadOnlyList<string>> _nodeNames;
    private readonly Lazy<Dictionary<Type, string[]>> _destinationNodeRoutes;

    /// <summary>
    /// Сборка топологии.
    /// </summary>
    public Assembly Assembly => _assembly;

    /// <summary>
    /// Список адресов роутеров.
    /// </summary>
    public IReadOnlyList<string> RouterAddresses => _routerAddresses.Value;

    /// <summary>
    /// Список имён узлов.
    /// </summary>
    public IReadOnlyList<string> NodeNames => _nodeNames.Value;

    /// <summary>
    /// Словарь: тип Payload -> список имён узлов назначения (из атрибутов DestinationNode).
    /// </summary>
    public IReadOnlyDictionary<Type, string[]> DestinationNodeRoutes => _destinationNodeRoutes.Value;

    /// <summary>
    /// Создаёт загрузчик топологии.
    /// </summary>
    /// <param name="assembly">Сборка топологии.</param>
    public TopologyLoader(Assembly assembly)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));

        _routerAddresses = new Lazy<IReadOnlyList<string>>(LoadRouterAddresses);
        _nodeNames = new Lazy<IReadOnlyList<string>>(LoadNodeNames);
        _destinationNodeRoutes = new Lazy<Dictionary<Type, string[]>>(LoadDestinationNodeRoutes);
    }

    /// <summary>
    /// Загружает список адресов роутеров из статического класса с константами.
    /// Ожидается наличие класса с именем "RouterAddresses" или атрибута AssemblyRouterAddress.
    /// </summary>
    private IReadOnlyList<string> LoadRouterAddresses()
    {
        var addresses = new List<string>();

        // Вариант 1: поиск статического класса с константами
        var routerClass = _assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "RouterAddresses" || t.Name == "TestRouterAddresses");

        if (routerClass != null)
        {
            var fields = routerClass.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(string) && field.GetValue(null) is string address)
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses.AsReadOnly();
    }

    /// <summary>
    /// Загружает список имён узлов из статического класса с константами.
    /// Ожидается наличие класса с именем "NodeNames" или "TestNodeNames".
    /// </summary>
    private IReadOnlyList<string> LoadNodeNames()
    {
        var names = new List<string>();

        var nodeClass = _assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "NodeNames" || t.Name == "TestNodeNames");

        if (nodeClass != null)
        {
            var fields = nodeClass.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(string) && field.GetValue(null) is string name)
                {
                    names.Add(name);
                }
            }
        }

        return names.AsReadOnly();
    }

    /// <summary>
    /// Загружает маршруты из атрибутов DestinationNode.
    /// </summary>
    private Dictionary<Type, string[]> LoadDestinationNodeRoutes()
    {
        var routes = new Dictionary<Type, string[]>();

        foreach (var type in _assembly.GetTypes())
        {
            var attributes = type.GetCustomAttributes<DestinationNodeAttribute>();
            var nodeNames = attributes.Select(a => a.NodeName).ToArray();
            if (nodeNames.Length > 0)
            {
                routes[type] = nodeNames;
            }
        }

        return routes;
    }

    /// <summary>
    /// Получает имена узлов для указанного типа Payload.
    /// </summary>
    public string[]? GetDestinationNodesForPayload(Type payloadType)
    {
        return DestinationNodeRoutes.TryGetValue(payloadType, out var nodes) ? nodes : null;
    }

    /// <summary>
    /// Получает все типы Payload, помеченные атрибутом DestinationNode.
    /// </summary>
    public IReadOnlyList<Type> GetPayloadTypes()
    {
        return DestinationNodeRoutes.Keys.ToList().AsReadOnly();
    }
}
