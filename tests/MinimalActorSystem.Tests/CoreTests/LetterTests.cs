using System.Reflection;

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
        TestLetter letter = new(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    /// <summary>
    /// Проверка: Sender и Receiver можно изменить после создания (поддержка паттерна аккумулятора)
    /// </summary>
    [Fact]
    public void LetterTests_002()
    {
        TestLetter letter = new(Sender, Receiver);
        Guid newSender = Guid.NewGuid();
        Guid newReceiver = Guid.NewGuid();

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
        TimeoutCallback callback = new(Receiver, 1, () => { });
        TimeServiceLetter letter = new(Sender, Receiver, callback);

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
        ShutdownLetter letter = new(Sender, Receiver);

        Assert.Equal(Sender, letter.Sender);
        Assert.Equal(Receiver, letter.Receiver);
    }

    /// <summary>
    /// Проверка: InitializeLetter правильно устанавливает Sender и Receiver
    /// </summary>
    [Fact]
    public void LetterTests_005()
    {
        InitializeLetter letter = new(Sender, Receiver);

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
        TimeServiceLetter timeLetter = new(Sender, Receiver, new TimeoutCallback(Receiver, 1, () => { }));
        ShutdownLetter shutdownLetter = new(Sender, Receiver);
        InitializeLetter initLetter = new(Sender, Receiver);

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
        TimeoutCallback callback = new(Receiver, 1, () => { });
        TimeServiceLetter letter = new(Sender, Receiver, callback);

        // Проверяем, что свойство Callback не имеет сеттера (компиляция проверит, но добавим runtime-проверку)
        PropertyInfo? property = typeof(TimeServiceLetter).GetProperty(nameof(TimeServiceLetter.Callback));
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
        TestLetter letter = new(Sender, Receiver);

        // Эмулируем получение письма и отправку ответа обратно с переставленными ролями
        Guid originalSender = letter.Sender;
        Guid originalReceiver = letter.Receiver;

        letter.Sender = originalReceiver;
        letter.Receiver = originalSender;

        Assert.Equal(originalReceiver, letter.Sender);
        Assert.Equal(originalSender, letter.Receiver);
    }

    #endregion
}
