namespace MinimalActorSystem.Tests.Core;

public sealed class SettingsTests
{
    // Проверка: значения по умолчанию
    [Fact]
    public void SettingsTests_001()
    {
        var settings = new Settings();

        Assert.True(settings.IsProduction);
        Assert.False(settings.SynchronousProcessing);
    }

    // Проверка: установка значений через init
    [Fact]
    public void SettingsTests_002()
    {
        var settings = new Settings
        {
            IsProduction = false,
            SynchronousProcessing = true
        };

        Assert.False(settings.IsProduction);
        Assert.True(settings.SynchronousProcessing);
    }
}
