using System.Text;
using System.Text.Json;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Тесты для фейковых реализаций транспорта и клиента маршрутизатора.
/// </summary>
public sealed class FakeTransportTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    #region FakeNetworkTransport Tests

    /// <summary>
    /// Проверка: отправка письма сохраняет его в истории
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_001()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var letter = new DefaultNetworkLetter("NodeB", "{\"data\":\"test\"}", "TestType");
        var targetUrl = "http://node-b/api/net";

        // Act
        await transport.SendAsync(targetUrl, letter, CancellationToken.None);

        // Assert
        var sentMessages = transport.GetSentMessages().ToList();
        Assert.Single(sentMessages);
        Assert.Equal(targetUrl, sentMessages[0].TargetNode);
        Assert.Equal("NodeB", sentMessages[0].Letter.ReceiverNodeName);
    }

    /// <summary>
    /// Проверка: метод WasSentTo возвращает true для отправленного узла
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_002()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var letter = new DefaultNetworkLetter("NodeB", "{}", "Test");
        var url = "http://target";

        // Act
        await transport.SendAsync(url, letter);
        var exists = transport.WasSentTo(url);

        // Assert
        Assert.True(exists);
        Assert.False(transport.WasSentTo("http://other"));
    }

    /// <summary>
    /// Проверка: ClearHistory очищает журнал отправок
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_003()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var letter = new DefaultNetworkLetter("Node", "{}", "Test");

        // Act
        await transport.SendAsync("url1", letter);
        await transport.SendAsync("url2", letter);
        transport.ClearHistory();

        // Assert
        Assert.Empty(transport.GetSentMessages());
        Assert.False(transport.WasSentTo("url1"));
    }

    /// <summary>
    /// Проверка: эмуляция входящего сообщения позволяет прочитать его через ReadIncomingAsync
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_004()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var letter = new DefaultNetworkLetter("Me", "{\"id\":1}", "Test");

        // Act
        transport.SimulateIncomingMessage(letter);
        var received = await transport.ReadIncomingAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(received);
        Assert.Equal("Me", received.ReceiverNodeName);
        Assert.Equal("{\"id\":1}", received.SerializedLetter);
    }

    /// <summary>
    /// Проверка: десериализация валидного JSON потока работает корректно
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_005()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var original = new DefaultNetworkLetter("Target", "{\"msg\":\"hi\"}", "MyType");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = await transport.DeserializeAsync(stream, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Target", result.ReceiverNodeName);
        Assert.Equal("{\"msg\":\"hi\"}", result.SerializedLetter);
    }

    /// <summary>
    /// Проверка: десериализация пустого потока возвращает null
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_006()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        using var stream = new MemoryStream();

        // Act
        var result = await transport.DeserializeAsync(stream, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверка: отмена токена отмены при отправке выбрасывает TaskCanceledException
    /// </summary>
    [Fact]
    public async Task FakeNetworkTransportTests_007()
    {
        // Arrange
        var transport = new FakeNetworkTransport();
        var letter = new DefaultNetworkLetter("Node", "{}", "Test");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await transport.SendAsync("url", letter, cts.Token));
    }

    #endregion

    #region FakeRouterClient Tests

    /// <summary>
    /// Проверка: регистрация узла успешна и возвращает таблицу маршрутизации
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_001()
    {
        // Arrange
        var service = new FakeRouterService();
        var client = new FakeRouterClient(service);

        // Act
        var table = await client.RegisterNodeAsync("NodeA", "http://node-a", CancellationToken.None);

        // Assert
        Assert.NotNull(table);
        Assert.Contains("NodeA", table.Keys);
        Assert.Equal("http://node-a", table["NodeA"]);
    }

    /// <summary>
    /// Проверка: получение таблицы маршрутизации возвращает все зарегистрированные узлы
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_002()
    {
        // Arrange
        var service = new FakeRouterService();
        var client = new FakeRouterClient(service);

        await service.RegisterNodeAsync("NodeA", "http://a", CancellationToken.None);
        await service.RegisterNodeAsync("NodeB", "http://b", CancellationToken.None);

        // Act
        var table = await client.GetResolutionTableAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, table.Count);
        Assert.Equal("http://a", table["NodeA"]);
        Assert.Equal("http://b", table["NodeB"]);
    }

    /// <summary>
    /// Проверка: heartbeat возвращает true для зарегистрированного узла
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_003()
    {
        // Arrange
        var service = new FakeRouterService();
        var client = new FakeRouterClient(service);

        await service.RegisterNodeAsync("NodeA", "http://a", CancellationToken.None);

        // Act
        var result = await client.SendHeartbeatAsync("NodeA", CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Проверка: heartbeat возвращает false для незарегистрированного узла
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_004()
    {
        // Arrange
        var service = new FakeRouterService();
        var client = new FakeRouterClient(service);

        // Act
        var result = await client.SendHeartbeatAsync("UnknownNode", CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Проверка: регистрация и запросы не работают, если сервис "упал" (IsDown = true)
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_005()
    {
        // Arrange
        var service = new FakeRouterService { IsDown = true };
        var client = new FakeRouterClient(service);

        // Act & Assert: Регистрация должна вернуть исключение или false (в нашей реализации - исключение из-за логики клиента)
        // В текущей реализации FakeRouterClient бросает InvalidOperationException при false от сервиса
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.RegisterNodeAsync("Node", "http://node", CancellationToken.None));

        // Heartbeat должен вернуть false
        var hbResult = await client.SendHeartbeatAsync("Node", CancellationToken.None);
        Assert.False(hbResult);
    }

    /// <summary>
    /// Проверка: очистка сервиса (Clear) удаляет все узлы
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_006()
    {
        // Arrange
        var service = new FakeRouterService();
        var client = new FakeRouterClient(service);

        await service.RegisterNodeAsync("NodeA", "http://a", CancellationToken.None);
        service.Clear();

        // Act
        var table = await client.GetResolutionTableAsync(CancellationToken.None);

        // Assert
        Assert.Empty(table);
    }

    /// <summary>
    /// Проверка: эмуляция задержки (Latency) увеличивает время выполнения операции
    /// </summary>
    [Fact]
    public async Task FakeRouterClientTests_007()
    {
        // Arrange
        var service = new FakeRouterService { Latency = TimeSpan.FromMilliseconds(100) };
        var client = new FakeRouterClient(service);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await client.RegisterNodeAsync("Node", "http://node", CancellationToken.None);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds >= 90); // Небольшой допуск на погрешность
    }

    #endregion
}
