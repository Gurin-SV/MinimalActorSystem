using FluentAssertions;
using MinimalActorSystem.XmlModel;

namespace MinimalActorSystem.Tests.CompiledModels;

public class CompiledModelTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestHelpers

    private enum TestMode
    {
        Slow,
        Fast
    }

    private class TestSensorActor(IActorSystem system, Guid uid, string name) : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private class TestStorage
    {
        public string Path { get; set; } = "";
    }

    private class TestCompiledModelActor(IActorSystem system, string xml) : CompiledModelActor(system)
    {
        private readonly string _xml = xml;
        private readonly TaskCompletionSource<bool> _buildCompleted = new();

        public Task BuildCompleted => _buildCompleted.Task;

        public new bool HasObject(Guid uid) => base.HasObject(uid);
        public new T GetObject<T>(Guid uid) where T : class => base.GetObject<T>(uid);

        protected override CompiledModel CompileModel()
        {
            var compiler = new XmlModelCompiler();
            return compiler.Compile(_xml);
        }

        protected override object CreateObject(ElementConfig element)
        {
            return element.ElementType switch
            {
                "Sensor" => new TestSensorActor(System, element.Uid, element.TryGetString("Name", out var n) ? n : ""),
                "Storage" => new TestStorage { Path = element.TryGetString("Path", out var p) ? p : "" },
                _ => throw new ArgumentException($"Unknown type: {element.ElementType}")
            };
        }

        protected override ValueTask OnModelLetter(Letter letter)
        {
            if (letter is BuildCompletedLetter)
            {
                _buildCompleted.TrySetResult(true);
            }
            return default;
        }
    }

    private sealed class BuildCompletedLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    #endregion

    #region Tests

    /// <summary>
    /// Проверяет, что иерархический XML разворачивается в плоский словарь,
    /// группирующие элементы пропускаются, элементы без Uid исключаются.
    /// </summary>
    [Fact]
    public void CompiledModelTests_001()
    {
        const string xml = @"
        <Root>
            <Sensor Uid=""00000000-0000-0000-0000-000000000001"" Name=""Датчик"">
                <PollInterval>1000</PollInterval>
            </Sensor>
            <Aggregator Uid=""00000000-0000-0000-0000-000000000002"" Name=""Агрегатор"" Method=""Avg"">
                <Source>
                    <SensorRef Uid=""00000000-0000-0000-0000-000000000003"" SensorUid=""00000000-0000-0000-0000-000000000001"" />
                </Source>
            </Aggregator>
        </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Aggregator", new ElementRule()
                .WithGroupElement("Source"));

        var model = compiler.Compile(xml);

        // Sensor, Aggregator, SensorRef (Source пропущен, PollInterval без Uid пропущен)
        model.Count.Should().Be(3);
        model.Uids.Should().HaveCount(3);
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001")).Should().NotBeNull();
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002")).Should().NotBeNull();
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000003")).Should().NotBeNull();
    }

    /// <summary>
    /// Проверяет, что вложенный элемент без атрибутов, содержащий только текст,
    /// сохраняется как свойство родителя.
    /// </summary>
    [Fact]
    public void CompiledModelTests_002()
    {
        const string xml = @"
        <Root>
            <Sensor Uid=""00000000-0000-0000-0000-000000000001"">
                <PollInterval>1000</PollInterval>
            </Sensor>
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);

        var sensor = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        sensor.Should().NotBeNull();
        sensor!.HasProperty("PollInterval").Should().BeTrue();
        sensor.TryGetString("PollInterval", out var value).Should().BeTrue();
        value.Should().Be("1000");
    }

    /// <summary>
    /// Проверяет, что атрибуты элемента, не указанные в правилах явно,
    /// считаются свойствами при использовании ElementRule.Default.
    /// </summary>
    [Fact]
    public void CompiledModelTests_003()
    {
        const string xml = @"
        <Root>
            <Config Uid=""00000000-0000-0000-0000-000000000001"" Host=""localhost"" Port=""8080"" Timeout=""30"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        var config = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        config.Should().NotBeNull();
        config!.HasProperty("Host").Should().BeTrue();
        config.TryGetString("Host", out var host).Should().BeTrue();
        host.Should().Be("localhost");
        config.TryGetString("Port", out var port).Should().BeTrue();
        port.Should().Be("8080");
        config.TryGetString("Timeout", out var timeout).Should().BeTrue();
        timeout.Should().Be("30");
    }

    /// <summary>
    /// Проверяет, что элемент с атрибутом Uid добавляется в модель,
    /// а элемент без Uid пропускается с предупреждением.
    /// </summary>
    [Fact]
    public void CompiledModelTests_004()
    {
        const string xml = @"
        <Root>
            <WithUid Uid=""00000000-0000-0000-0000-000000000001"" Name=""Явный"" />
            <WithoutUid Name=""Авто"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        var withUid = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        withUid.Should().NotBeNull();
        withUid!.TryGetString("Name", out var name).Should().BeTrue();
        name.Should().Be("Явный");

        compiler.Warnings.Should().HaveCount(1);
        compiler.Warnings[0].Should().Contain("missing Uid attribute");
    }

    /// <summary>
    /// Проверяет, что один и тот же элемент (по Uid) добавляется в модель только один раз,
    /// даже если встречается в XML многократно. Группирующие элементы в модель не попадают.
    /// </summary>
    [Fact]
    public void CompiledModelTests_005()
    {
        const string xml = @"
        <Root>
            <Shared Uid=""00000000-0000-0000-0000-000000000001"" Name=""Общий"" />
            <Consumer Uid=""00000000-0000-0000-0000-000000000002"">
                <Source>
                    <Shared Uid=""00000000-0000-0000-0000-000000000001"" />
                </Source>
            </Consumer>
        </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Consumer", new ElementRule()
                .WithGroupElement("Source"));

        var model = compiler.Compile(xml);

        // Shared и Consumer — по одному разу
        model.Count.Should().Be(2);
        var sharedCount = model.Uids.Count(uid => uid == Guid.Parse("00000000-0000-0000-0000-000000000001"));
        sharedCount.Should().Be(1);
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002")).Should().NotBeNull();
    }

    /// <summary>
    /// Проверяет, что атрибут Source сохраняется как обычное свойство элемента.
    /// Построение связей по значению Source — задача прикладного кода.
    /// </summary>
    [Fact]
    public void CompiledModelTests_006()
    {
        const string xml = @"
        <Root>
            <Aggregator Uid=""00000000-0000-0000-0000-000000000001"" Source=""00000000-0000-0000-0000-000000000002"" />
            <Sensor Uid=""00000000-0000-0000-0000-000000000002"" Name=""Датчик"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(2);

        var aggregator = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        aggregator.Should().NotBeNull();
        aggregator!.TryGetString("Source", out var source).Should().BeTrue();
        source.Should().Be("00000000-0000-0000-0000-000000000002");

        var sensor = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        sensor.Should().NotBeNull();
        sensor!.TryGetString("Name", out var name).Should().BeTrue();
        name.Should().Be("Датчик");
    }

    /// <summary>
    /// Проверяет, что элементы могут быть вложены друг в друга,
    /// все атрибуты сохраняются как свойства, вложенность не влияет на связи.
    /// </summary>
    [Fact]
    public void CompiledModelTests_007()
    {
        const string xml = @"
        <Root>
            <Aggregator Uid=""00000000-0000-0000-0000-000000000001"" Source=""00000000-0000-0000-0000-000000000002"">
                <Sensor Uid=""00000000-0000-0000-0000-000000000002"" Name=""Датчик"" />
            </Aggregator>
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(2);

        var sensor = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        sensor.Should().NotBeNull();
        sensor!.TryGetString("Name", out var name).Should().BeTrue();
        name.Should().Be("Датчик");

        var aggregator = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        aggregator.Should().NotBeNull();
        aggregator!.TryGetString("Source", out var source).Should().BeTrue();
        source.Should().Be("00000000-0000-0000-0000-000000000002");
    }

    /// <summary>
    /// Проверяет, что элемент без атрибута Uid пропускается с предупреждением,
    /// а остальные элементы модели создаются нормально.
    /// </summary>
    [Fact]
    public void CompiledModelTests_008()
    {
        const string xml = @"
            <Root>
                <Sensor Name=""Без Uid"" />
                <Sensor Uid=""00000000-0000-0000-0000-000000000001"" Name=""С Uid"" />
            </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001")).Should().NotBeNull();

        compiler.Warnings.Should().HaveCount(1);
        compiler.Warnings[0].Should().Contain("missing Uid attribute");
    }

    /// <summary>
    /// Проверяет, что вложенный элемент без атрибутов и с текстом сохраняется как свойство родителя.
    /// Элемент с атрибутами — полноценный элемент, обязан иметь Uid.
    /// </summary>
    [Fact]
    public void CompiledModelTests_009()
    {
        const string xml = @"
        <Root>
            <Config Uid=""00000000-0000-0000-0000-000000000001"">
                <Host>localhost</Host>
                <Port>8080</Port>
                <Database Uid=""00000000-0000-0000-0000-000000000002"" Name=""MainDB"" />
            </Config>
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        model.Count.Should().Be(2);

        var config = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        config.Should().NotBeNull();
        config!.TryGetString("Host", out var host).Should().BeTrue();
        host.Should().Be("localhost");
        config.TryGetString("Port", out var port).Should().BeTrue();
        port.Should().Be("8080");

        var database = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        database.Should().NotBeNull();
        database!.TryGetString("Name", out var name).Should().BeTrue();
        name.Should().Be("MainDB");
    }

    /// <summary>
    /// Проверяет типизированное чтение свойств: string, int, long, double, bool, Guid, enum.
    /// Включая разные форматы чисел: десятичный, шестнадцатеричный, двоичный, восьмеричный.
    /// </summary>
    [Fact]
    public void CompiledModelTests_010()
    {
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
        <Root>
            <TypedConfig Uid=""00000000-0000-0000-0000-000000000001""
                Name=""Тест""
                MaxCount=""100""
                HexCount=""0xFF""
                BinCount=""0b1010""
                OctCount=""0o77""
                Total=""9999999999""
                HexTotal=""0xFFFFFFFF""
                Factor=""3.14""
                Enabled=""true""
                Disabled=""false""
                SourceId=""00000000-0000-0000-0000-000000000002""
                Mode=""Fast"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        var config = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        config.Should().NotBeNull();

        config!.TryGetString("Name", out var name).Should().BeTrue();
        name.Should().Be("Тест");

        config.TryGetInt32("MaxCount", out var maxCount).Should().BeTrue();
        maxCount.Should().Be(100);

        config.TryGetInt32("HexCount", out var hexCount).Should().BeTrue();
        hexCount.Should().Be(255);

        config.TryGetInt32("BinCount", out var binCount).Should().BeTrue();
        binCount.Should().Be(10);

        config.TryGetInt32("OctCount", out var octCount).Should().BeTrue();
        octCount.Should().Be(63);

        config.TryGetInt64("Total", out var total).Should().BeTrue();
        total.Should().Be(9999999999L);

        config.TryGetInt64("HexTotal", out var hexTotal).Should().BeTrue();
        hexTotal.Should().Be(0xFFFFFFFFL);

        config.TryGetDouble("Factor", out var factor).Should().BeTrue();
        factor.Should().BeApproximately(3.14, 0.001);

        config.TryGetBoolean("Enabled", out var enabled).Should().BeTrue();
        enabled.Should().BeTrue();

        config.TryGetBoolean("Disabled", out var disabled).Should().BeTrue();
        disabled.Should().BeFalse();

        config.TryGetGuid("SourceId", out var sourceId).Should().BeTrue();
        sourceId.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        config.TryGetEnum<TestMode>("Mode", out var mode).Should().BeTrue();
        mode.Should().Be(TestMode.Fast);
    }

    /// <summary>
    /// Проверяет интеграцию CompiledModelActor с акторной системой:
    /// объекты с типом Actor регистрируются как акторы,
    /// обычные объекты сохраняются в словаре.
    /// </summary>
    [Fact]
    public async Task CompiledModelTests_011()
    {
        const string xml = @"
        <Root>
            <Sensor Uid=""00000000-0000-0000-0000-000000000011"" Name=""Датчик"" />
            <Storage Uid=""00000000-0000-0000-0000-000000000012"" Path=""/data"" />
        </Root>";

        var settings = new Settings { TimeServiceModes = TimeServiceModes.Sync };
        var system = new ActorSystem(settings);
        var modelActor = new TestCompiledModelActor(system, xml);
        system.RegisterActor(modelActor);

        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));
        system.Send(new BuildCompletedLetter(SystemUids.System, SystemUids.Model));
        await modelActor.BuildCompleted.WaitAsync(TimeSpan.FromSeconds(5));

        system.ActorCount.Should().Be(2);

        system.FindActor(Guid.Parse("00000000-0000-0000-0000-000000000011")).Should().NotBeNull();
        system.FindActor(Guid.Parse("00000000-0000-0000-0000-000000000012")).Should().BeNull();

        modelActor.HasObject(Guid.Parse("00000000-0000-0000-0000-000000000012")).Should().BeTrue();
        var storage = modelActor.GetObject<TestStorage>(Guid.Parse("00000000-0000-0000-0000-000000000012"));
        storage.Path.Should().Be("/data");
    }

    /// <summary>
    /// Проверяет, что ElementRule.WithProperty ограничивает набор свойств:
    /// только явно указанные атрибуты попадают в ElementConfig.
    /// </summary>
    [Fact]
    public void CompiledModelTests_012()
    {
        const string xml = @"
    <Root>
        <Config Uid=""00000000-0000-0000-0000-000000000001"" 
                Host=""localhost"" 
                Port=""8080"" 
                Timeout=""30"" />
    </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Config", new ElementRule().WithProperties("Host", "Port"));

        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        var config = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        config.Should().NotBeNull();

        config!.HasProperty("Host").Should().BeTrue();
        config.HasProperty("Port").Should().BeTrue();
        config.HasProperty("Timeout").Should().BeFalse();
    }

    /// <summary>
    /// Проверяет, что WithRequired в комбинации с WithProperties
    /// пропускает элемент, если обязательное свойство отсутствует.
    /// </summary>
    [Fact]
    public void CompiledModelTests_013()
    {
        const string xml = @"
    <Root>
        <Database Uid=""00000000-0000-0000-0000-000000000001"" 
                  Name=""MainDB"" />
        <Database Uid=""00000000-0000-0000-0000-000000000002"" 
                  Name=""BackupDB"" 
                  ConnectionString=""Server=backup"" />
    </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Database", new ElementRule()
                .WithProperties("Name")
                .WithRequired("ConnectionString"));

        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001")).Should().BeNull();
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002")).Should().NotBeNull();

        compiler.Warnings.Should().HaveCount(1);
        compiler.Warnings[0].Should().Contain("missing required properties");
        compiler.Warnings[0].Should().Contain("ConnectionString");
    }

    /// <summary>
    /// Проверяет, что WithRequired без WithProperties автоматически
    /// добавляет обязательное свойство в список разрешённых.
    /// </summary>
    [Fact]
    public void CompiledModelTests_014()
    {
        const string xml = @"
    <Root>
        <Service Uid=""00000000-0000-0000-0000-000000000001"" 
                 Endpoint=""https://api.example.com"" 
                 Version=""v2"" />
    </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Service", new ElementRule()
                .WithRequired("Endpoint"));

        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        var service = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        service.Should().NotBeNull();

        service!.HasProperty("Endpoint").Should().BeTrue();
        service.HasProperty("Version").Should().BeFalse();
    }

    /// <summary>
    /// Проверяет, что WithRequired + WithProperties вместе
    /// фильтруют атрибуты и требуют обязательные.
    /// </summary>
    [Fact]
    public void CompiledModelTests_015()
    {
        const string xml = @"
    <Root>
        <Worker Uid=""00000000-0000-0000-0000-000000000001"" 
                Name=""worker1"" 
                Threads=""4"" 
                Debug=""true"" />
    </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Worker", new ElementRule()
                .WithProperties("Name", "Threads")
                .WithRequired("Name"));

        var model = compiler.Compile(xml);

        model.Count.Should().Be(1);
        var worker = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        worker.Should().NotBeNull();

        worker!.HasProperty("Name").Should().BeTrue();
        worker.HasProperty("Threads").Should().BeTrue();
        worker.HasProperty("Debug").Should().BeFalse();
    }

    /// <summary>
    /// Проверяет, что множественные группирующие элементы обрабатываются независимо.
    /// </summary>
    [Fact]
    public void CompiledModelTests_016()
    {
        const string xml = @"
    <Root>
        <Inputs>
            <Sensor Uid=""00000000-0000-0000-0000-000000000001"" Name=""Sensor1"" />
        </Inputs>
        <Outputs>
            <Actuator Uid=""00000000-0000-0000-0000-000000000002"" Name=""Actuator1"" />
        </Outputs>
    </Root>";

        var compiler = new XmlModelCompiler()
            .AddRule("Inputs", new ElementRule().WithGroupElement("Inputs"))
            .AddRule("Outputs", new ElementRule().WithGroupElement("Outputs"));

        var model = compiler.Compile(xml);

        model.Count.Should().Be(2);
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001")).Should().NotBeNull();
        model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002")).Should().NotBeNull();
    }

    #endregion
}
