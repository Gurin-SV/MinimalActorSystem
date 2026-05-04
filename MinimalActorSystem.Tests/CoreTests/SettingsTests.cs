namespace MinimalActorSystem.Tests;

public sealed class SettingsTests
{
    // Проверка: значения по умолчанию
    [Fact]
    public void SettingsTests_001()
    {
        var settings = new Settings();

        Assert.Equal(200, settings.DefaultQueueCapacity);
        Assert.True(settings.IsProduction);
        Assert.False(settings.SynchronousProcessing);
    }

    // Проверка: установка значений через init
    [Fact]
    public void SettingsTests_002()
    {
        var settings = new Settings
        {
            DefaultQueueCapacity = 500,
            IsProduction = false,
            SynchronousProcessing = true
        };

        Assert.Equal(500, settings.DefaultQueueCapacity);
        Assert.False(settings.IsProduction);
        Assert.True(settings.SynchronousProcessing);
    }
}
