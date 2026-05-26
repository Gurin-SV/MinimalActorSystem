namespace MinimalActorSystem.Tests.Core;

public sealed class LetterTests
{
    private static readonly Guid Sender = Guid.NewGuid();
    private static readonly Guid Receiver = Guid.NewGuid();

    #region TestLetters

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    #endregion

    #region Tests

    /// <summary>
    /// Проверка: конструктор базового письма устанавливает Sender и Receiver
    /// </summary>
    [Fact]
    public void LetterTests_001()
    {
        var letter = new TestLetter(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    /// <summary>
    /// Проверка: Sender и Receiver можно изменить после создания (поддержка паттерна аккумулятора)
    /// </summary>
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

    /// <summary>
    /// Проверка: TimeServiceLetter сохраняет Sender, Receiver и Callback
    /// </summary>
    [Fact]
    public void LetterTests_003()
    {
        var callback = new TimeoutCallback(Receiver, 1, () => { });
        var letter = new TimeServiceLetter(Sender, Receiver, callback);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
        Assert.Equal(callback, letter.Callback);
    }

    /// <summary>
    /// Проверка: ShutdownLetter правильно устанавливает Sender и Receiver
    /// </summary>
    [Fact]
    public void LetterTests_004()
    {
        var letter = new ShutdownLetter(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    /// <summary>
    /// Проверка: InitializeLetter правильно устанавливает Sender и Receiver
    /// </summary>
    [Fact]
    public void LetterTests_005()
    {
        var letter = new InitializeLetter(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    /// <summary>
    /// Проверка: разные типы писем с одинаковыми Sender/Receiver считаются разными объектами
    /// (но равенство не переопределено, поэтому сравниваются ссылки)
    /// </summary>
    [Fact]
    public void LetterTests_006()
    {
        var timeLetter = new TimeServiceLetter(Sender, Receiver, new TimeoutCallback(Receiver, 1, () => { }));
        var shutdownLetter = new ShutdownLetter(Sender, Receiver);
        var initLetter = new InitializeLetter(Sender, Receiver);

        Assert.NotSame(timeLetter, shutdownLetter);
        Assert.NotSame(timeLetter, initLetter);
        Assert.NotSame(shutdownLetter, initLetter);
    }

    /// <summary>
    /// Проверка: TimeServiceLetter.Callback доступен только для чтения (нельзя изменить после создания)
    /// </summary>
    [Fact]
    public void LetterTests_007()
    {
        var callback = new TimeoutCallback(Receiver, 1, () => { });
        var letter = new TimeServiceLetter(Sender, Receiver, callback);

        // Проверяем, что свойство Callback не имеет сеттера (компиляция проверит, но добавим runtime-проверку)
        var property = typeof(TimeServiceLetter).GetProperty(nameof(TimeServiceLetter.Callback));
        Assert.NotNull(property);
        Assert.Null(property.GetSetMethod()); // Нет публичного сеттера
    }

    /// <summary>
    /// Проверка: мутабельность Sender и Receiver полезна для паттерна аккумулятора
    /// (письмо может быть отправлено обратно с изменёнными ролями)
    /// </summary>
    [Fact]
    public void LetterTests_008()
    {
        var letter = new TestLetter(Sender, Receiver);

        // Эмулируем получение письма и отправку ответа обратно с переставленными ролями
        var originalSender = letter.Sender;
        var originalReceiver = letter.Receiver;

        letter.Sender = originalReceiver;
        letter.Receiver = originalSender;

        Assert.Equal(originalReceiver, letter.Sender);
        Assert.Equal(originalSender, letter.Receiver);
    }

    #endregion
}
