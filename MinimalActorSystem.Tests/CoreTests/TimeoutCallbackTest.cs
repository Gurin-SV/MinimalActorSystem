namespace MinimalActorSystem.Tests.Core;

public sealed class TimeoutCallbackTests
{
    private static readonly Guid ActorUid = Guid.NewGuid();

    // Проверка: коллбеки с одинаковыми ActorUid и CallbackId равны
    [Fact]
    public void TimeoutCallbackTests_001()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 1, () => { });

        Assert.Equal(cb1, cb2);
        Assert.True(cb1 == cb2);
    }

    // Проверка: коллбеки с разными ActorUid не равны
    [Fact]
    public void TimeoutCallbackTests_002()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(Guid.NewGuid(), 1, () => { });

        Assert.NotEqual(cb1, cb2);
        Assert.False(cb1 == cb2);
    }

    // Проверка: коллбеки с разными CallbackId не равны
    [Fact]
    public void TimeoutCallbackTests_003()
    {
        var cb1 = new TimeoutCallback(ActorUid, 1, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 2, () => { });

        Assert.NotEqual(cb1, cb2);
        Assert.False(cb1 == cb2);
    }

    // Проверка: одинаковые коллбеки имеют одинаковый хэш-код
    [Fact]
    public void TimeoutCallbackTests_004()
    {
        var cb1 = new TimeoutCallback(ActorUid, 42, () => { });
        var cb2 = new TimeoutCallback(ActorUid, 42, () => { });

        Assert.Equal(cb1.GetHashCode(), cb2.GetHashCode());
    }

    // Проверка: Action можно вызвать
    [Fact]
    public void TimeoutCallbackTests_005()
    {
        var invoked = false;
        var cb = new TimeoutCallback(ActorUid, 1, () => invoked = true);

        cb.Action();

        Assert.True(invoked);
    }
}
