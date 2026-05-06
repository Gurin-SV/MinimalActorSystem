namespace MinimalActorSystem.Tests.Core;

public sealed class LetterTests
{
    private static readonly Guid Sender = Guid.NewGuid();
    private static readonly Guid Receiver = Guid.NewGuid();

    // Проверка: конструктор устанавливает Sender и Receiver
    [Fact]
    public void LetterTests_001()
    {
        var letter = new TestLetter(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    // Проверка: Sender и Receiver можно изменить после создания
    [Fact]
    public void LetterTests_002()
    {
        var letter = new TestLetter(Sender, Receiver);
        var newSender = Guid.NewGuid();
        var newReceiver = Guid.NewGuid();

        letter.Sender = newSender;
        letter.Receiver = newReceiver;

        Assert.Equal(newSender, letter.Sender);
        Assert.Equal(newReceiver, letter.Receiver);
    }

    // Проверка: TimeServiceLetter сохраняет Sender, Receiver и Callback
    [Fact]
    public void LetterTests_003()
    {
        var callback = new TimeoutCallback(Receiver, 1, () => { });
        var letter = new TimeServiceLetter(Sender, Receiver, callback);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
        Assert.Equal(callback, letter.Callback);
    }

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);
}
