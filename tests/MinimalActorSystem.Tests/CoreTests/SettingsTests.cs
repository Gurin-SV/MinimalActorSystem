namespace MinimalActorSystem.Tests.Core;

public sealed class SettingsTests
{
    /// <summary>
    /// Проверка: значения по умолчанию
    /// </summary>
    [Fact]
    public void SettingsTests_001()
    {
        Settings settings = new();

        Assert.Equal(TimeServiceModes.System, settings.TimeServiceModes);
    }

    /// <summary>
    /// Проверка: установка значений через init
    /// </summary>
    [Fact]
    public void SettingsTests_002()
    {
        Settings settings = new()
        {
            TimeServiceModes = TimeServiceModes.Sync
        };

        Assert.Equal(TimeServiceModes.Sync, settings.TimeServiceModes);
    }

    /// <summary>
    /// Проверка: установка режима Async через init
    /// </summary>
    [Fact]
    public void SettingsTests_003()
    {
        Settings settings = new()
        {
            TimeServiceModes = TimeServiceModes.Async
        };

        Assert.Equal(TimeServiceModes.Async, settings.TimeServiceModes);
    }

    /// <summary>
    /// Проверка: несколько настроек можно создавать независимо
    /// </summary>
    [Fact]
    public void SettingsTests_004()
    {
        Settings settings1 = new();
        Settings settings2 = new() { TimeServiceModes = TimeServiceModes.Sync };
        Settings settings3 = new() { TimeServiceModes = TimeServiceModes.Async };

        Assert.Equal(TimeServiceModes.System, settings1.TimeServiceModes);
        Assert.Equal(TimeServiceModes.Sync, settings2.TimeServiceModes);
        Assert.Equal(TimeServiceModes.Async, settings3.TimeServiceModes);
    }

    /// <summary>
    /// Проверка: настройки иммутабельны после создания (init только при создании)
    /// </summary>
    [Fact]
    public void SettingsTests_005()
    {
        Settings settings = new() { TimeServiceModes = TimeServiceModes.Sync };

        // Проверяем, что значение установлено
        Assert.Equal(TimeServiceModes.Sync, settings.TimeServiceModes);

        // Значение нельзя изменить после инициализации (нет сеттера)
        // settings.TimeServiceModes = TimeServiceModes.Async; // Не компилируется
    }
}
