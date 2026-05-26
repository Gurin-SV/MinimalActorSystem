using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network.Tests.Actors;

/// <summary>
/// Тестовая реализация NetworkActor для использования в тестах.
/// Позволяет программно настраивать маршруты.
/// </summary>
public class TestNetworkActor : NetworkActor
{
    private readonly Action<TestNetworkActor>? _configureRoutingAction;

    public TestNetworkActor(
        IActorSystem system,
        IEnumerable<string> routerAddresses,
        Action<TestNetworkActor>? configureRouting = null,
        int queueCapacity = 512)
        : base(system, routerAddresses, queueCapacity)
    {
        _configureRoutingAction = configureRouting;
        System.Logger.LogDebug("TestNetworkActor created");
    }

    public void SetupRouting()
    {
        _configureRoutingAction?.Invoke(this);
        System.Logger.LogDebug("TestNetworkActor routing configured. Outbound routes: {OutboundCount}, Inbound routes: {InboundCount}",
            OutboundRoutes.Count, InboundRoutes.Count);
    }

    public void AddOutboundRoute<TPayload>(params string[] destinationNodeNames)
    {
        RegisterOutboundRoute<TPayload>(destinationNodeNames);
    }

    public void AddInboundRoute<TPayload>(Guid localActorUid)
    {
        RegisterInboundRoute<TPayload>(localActorUid);
    }

    public void ResetRouting()
    {
        OutboundRoutes.Clear();
        InboundRoutes.Clear();
        System.Logger.LogDebug("TestNetworkActor routing reset");
    }
}
