namespace MinimalActorSystem.Tests.Core;

public sealed class SettingsTests
{
    // Проверка: значения по умолчанию
    [Fact]
    public void SettingsTests_001()
    {
        var settings = new Settings();

        Assert.Equal(TimeServiceModes.System, settings.TimeServiceModes);
    }

    // Проверка: установка значений через init
    [Fact]
    public void SettingsTests_002()
    {
        var settings = new Settings
        {
            TimeServiceModes = TimeServiceModes.Sync
        };

        Assert.Equal(TimeServiceModes.Sync, settings.TimeServiceModes);
    }
}
