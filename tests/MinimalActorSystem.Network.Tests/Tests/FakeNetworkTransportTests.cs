using System.Collections.Concurrent;

namespace MinimalActorSystem.Network.Tests.Fakes;

/// <summary>
/// Дополнительные тесты для FakeNetworkTransport (крайние случаи).
/// </summary>
public sealed class FakeNetworkTransportExtraTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Проверка: SendAsync с отменой токена корректно обрабатывает отмену.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_001()
    {
        _output.WriteLine("Test 001: SendAsync handles cancellation");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();
        var cts = new CancellationTokenSource();

        using var sender = new FakeNetworkTransport(registry, system);
        using var receiver = new FakeNetworkTransport(registry, system);

        await sender.RegisterNodeAsync("Sender");
        await receiver.RegisterNodeAsync("Receiver");

        var tcs = new TaskCompletionSource<bool>();
        receiver.SetMessageHandler((source, message) =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        cts.Cancel();

        // Отправка с отменённым токеном должна завершиться без ошибки (но сообщение может не дойти)
        await sender.SendAsync("Receiver", "Test", cts.Token);

        // Assert - не должно быть исключения
        _output.WriteLine("Send with cancelled token completed without exception");
    }

    /// <summary>
    /// Проверка: RegisterNodeAsync с пустым именем выбрасывает исключение.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_002()
    {
        _output.WriteLine("Test 002: RegisterNodeAsync throws on empty node name");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();
        using var transport = new FakeNetworkTransport(registry, system);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.RegisterNodeAsync(""));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.RegisterNodeAsync("   "));

        _output.WriteLine("Exceptions thrown as expected");
    }

    /// <summary>
    /// Проверка: SetMessageHandler с null выбрасывает исключение.
    /// </summary>
    [Fact]
    public void FakeNetworkTransportExtraTests_003()
    {
        _output.WriteLine("Test 003: SetMessageHandler throws on null handler");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();
        using var transport = new FakeNetworkTransport(registry, system);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            transport.SetMessageHandler(null!));

        _output.WriteLine("Exception thrown as expected");
    }

    /// <summary>
    /// Проверка: Множественная регистрация одного узла обновляет реестр.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_004()
    {
        _output.WriteLine("Test 004: Multiple registration of same node updates registry");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();

        using var transport = new FakeNetworkTransport(registry, system);
        await transport.RegisterNodeAsync("TestNode");

        // Act
        await transport.RegisterNodeAsync("TestNode");

        // Assert
        Assert.True(transport.IsRegistered);
        Assert.Equal("TestNode", transport.CurrentNodeName);
        Assert.True(registry.ContainsKey("TestNode"));

        _output.WriteLine("Node re-registered successfully");
    }

    /// <summary>
    /// Проверка: SendAsync после Dispose выбрасывает ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_005()
    {
        _output.WriteLine("Test 005: SendAsync after Dispose throws ObjectDisposedException");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();

        var transport = new FakeNetworkTransport(registry, system);
        await transport.RegisterNodeAsync("Sender");

        // Act
        transport.Dispose();

        // Assert - после Dispose должен быть ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            transport.SendAsync("Receiver", "Test"));

        _output.WriteLine("ObjectDisposedException thrown as expected");
    }

    /// <summary>
    /// Проверка: SendAsync с пустым именем узла выбрасывает исключение.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_006()
    {
        _output.WriteLine("Test 006: SendAsync throws on empty node name");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();
        using var transport = new FakeNetworkTransport(registry, system);
        await transport.RegisterNodeAsync("Sender");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.SendAsync("", "Test"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.SendAsync("   ", "Test"));

        _output.WriteLine("Exceptions thrown as expected");
    }

    /// <summary>
    /// Проверка: Регистрация нескольких узлов с одинаковыми именами не создаёт дубликатов.
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportExtraTests_007()
    {
        _output.WriteLine("Test 007: Multiple registration of same node name does not create duplicates");

        // Arrange
        ActorSystem system = SystemFactory.CreateSystem(_output);
        var registry = new ConcurrentDictionary<string, FakeNetworkTransport>();

        using var transport1 = new FakeNetworkTransport(registry, system);
        using var transport2 = new FakeNetworkTransport(registry, system);

        // Act
        await transport1.RegisterNodeAsync("SameNode");
        await transport2.RegisterNodeAsync("SameNode");

        // Assert
        Assert.Single(registry);
        Assert.True(registry.ContainsKey("SameNode"));

        _output.WriteLine($"Registry count: {registry.Count}");
    }
}
