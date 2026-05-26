namespace MinimalActorSystem.Tests.Core;

public sealed class TimeoutCallbackTests
{
    private static readonly Guid ActorUid = Guid.NewGuid();

    /// <summary>
    /// Проверка: коллбеки с одинаковыми ActorUid и CallbackId равны
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_001()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 1, () => { });

        Assert.Equal(cb1, cb2);
        Assert.True(cb1 == cb2);
    }

    /// <summary>
    /// Проверка: коллбеки с разными ActorUid не равны
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_002()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(Guid.NewGuid(), 1, () => { });

        Assert.NotEqual(cb1, cb2);
        Assert.False(cb1 == cb2);
    }

    /// <summary>
    /// Проверка: коллбеки с разными CallbackId не равны
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_003()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 2, () => { });

        Assert.NotEqual(cb1, cb2);
        Assert.False(cb1 == cb2);
    }

    /// <summary>
    /// Проверка: одинаковые коллбеки имеют одинаковый хэш-код
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_004()
    {
        var cb1 = new TimeoutCallback(ActorUid, 42, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 42, () => { });

        Assert.Equal(cb1.GetHashCode(), cb2.GetHashCode());
    }

    /// <summary>
    /// Проверка: Action можно вызвать
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_005()
    {
        var invoked = false;
        var cb = new TimeoutCallback(ActorUid, 1, () => invoked = true);

        cb.Action();

        Assert.True(invoked);
    }

    /// <summary>
    /// Проверка: Equals(null) возвращает false
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_006()
    {
        var cb = new TimeoutCallback(ActorUid, 1, () => { });

        Assert.False(cb.Equals(null));
    }

    /// <summary>
    /// Проверка: Equals с объектом другого типа возвращает false
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_007()
    {
        var cb = new TimeoutCallback(ActorUid, 1, () => { });
        var notCallback = "some string";

        Assert.False(cb.Equals(notCallback));
    }

    /// <summary>
    /// Проверка: разные Action не влияют на равенство
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_008()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 1, () => Console.WriteLine("different"));

        Assert.Equal(cb1, cb2);
        Assert.True(cb1 == cb2);
    }

    /// <summary>
    /// Проверка: оператор != работает корректно
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_009()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb3 = new TimeoutCallback(ActorUid, 2, () => { });

        Assert.False(cb1 != cb2); // одинаковые — не должны быть не равны
        Assert.True(cb1 != cb3);  // разные — должны быть не равны
    }

    /// <summary>
    /// Проверка: GetHashCode стабилен для одинаковых значений
    /// </summary>
    [Fact]
    public void TimeoutCallbackTests_010()
    {
        var cb = new TimeoutCallback(ActorUid, 42, () => { });
        var firstHash = cb.GetHashCode();
        var secondHash = cb.GetHashCode();

        Assert.Equal(firstHash, secondHash);
    }
}
