using FluentAssertions;
using MinimalActorSystem.CompiledModels;

namespace MinimalActorSystem.Tests.CompiledModel;

public class CompiledModelTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Проверяет, что иерархический XML разворачивается в плоский словарь,
    /// содержащий все элементы независимо от уровня вложенности.
    /// </summary>
    [Fact]
    public void CompiledModelTest001()
    {
        const string xml = @"
            <Root>
                <Sensor Uid=""00000000-0000-0000-0000-000000000001"" Name=""Датчик"">
                    <PollInterval>1000</PollInterval>
                </Sensor>
                <Aggregator Uid=""00000000-0000-0000-0000-000000000002"" Name=""Агрегатор"" Method=""Avg"">
                    <Source>
                        <SensorRef SensorUid=""00000000-0000-0000-0000-000000000001"" />
                    </Source>
                </Aggregator>
            </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        // Должны быть: Sensor, PollInterval, Aggregator, Source, SensorRef
        model.Count.Should().Be(5);
        model.Uids.Should().HaveCount(5);
    }

    /// <summary>
    /// Проверяет, что текстовое содержимое элемента сохраняется как свойство
    /// с ключом, равным имени элемента.
    /// </summary>
    [Fact]
    public void CompiledModelTest002()
    {
        const string xml = @"
        <Root>
            <Sensor Uid=""00000000-0000-0000-0000-000000000001"">
                <PollInterval>1000</PollInterval>
            </Sensor>
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        var pollInterval = model.Uids
            .Select(uid => model.FindElement(uid))
            .First(e => e!.ElementType == "PollInterval");

        pollInterval.Should().NotBeNull();
        pollInterval!.Properties.Should().ContainKey("PollInterval");
        pollInterval.Properties["PollInterval"].Should().Be("1000");
    }

    /// <summary>
    /// Проверяет, что атрибуты элемента, не указанные в правилах явно,
    /// считаются свойствами при использовании ElementRule.Default.
    /// </summary>
    [Fact]
    public void CompiledModelTest003()
    {
        const string xml = @"
        <Root>
            <Config Uid=""00000000-0000-0000-0000-000000000001"" Host=""localhost"" Port=""8080"" Timeout=""30"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        var config = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        config.Should().NotBeNull();
        config!.Properties.Should().Contain("Host", "localhost");
        config.Properties.Should().Contain("Port", "8080");
        config.Properties.Should().Contain("Timeout", "30");
    }

    /// <summary>
    /// Проверяет, что атрибут Uid элемента распознаётся и используется как идентификатор.
    /// Если Uid не указан, компилятор генерирует новый Guid.
    /// </summary>
    [Fact]
    public void CompiledModelTest004()
    {
        const string xml = @"
        <Root>
            <WithUid Uid=""00000000-0000-0000-0000-000000000001"" Name=""Явный"" />
            <WithoutUid Name=""Авто"" />
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        var withUid = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        withUid.Should().NotBeNull();
        withUid!.Properties["Name"].Should().Be("Явный");

        var withoutUid = model.Uids
            .Select(uid => model.FindElement(uid))
            .First(e => e!.Properties["Name"] == "Авто");

        withoutUid.Should().NotBeNull();
        withoutUid!.Uid.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Проверяет, что один и тот же элемент (по Uid) добавляется в модель только один раз,
    /// даже если встречается в XML многократно.
    /// </summary>
    [Fact]
    public void CompiledModelTest005()
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

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        // Shared должен быть только один
        var sharedCount = model.Uids.Count(uid => uid == Guid.Parse("00000000-0000-0000-0000-000000000001"));
        sharedCount.Should().Be(1);
        model.Count.Should().Be(3); // Shared, Consumer, Source
    }

    /// <summary>
    /// Проверяет, что связь источник-потребитель устанавливается через атрибут Source.
    /// Aggregator.Source = Sensor.Uid означает, что Sensor является источником для Aggregator.
    /// </summary>
    [Fact]
    public void CompiledModelTest006()
    {
        const string xml = @"
        <Root>
            <Aggregator Uid=""00000000-0000-0000-0000-000000000001"" Source=""00000000-0000-0000-0000-000000000002"">
                <Sensor Uid=""00000000-0000-0000-0000-000000000002"" Name=""Датчик"" />
            </Aggregator>
        </Root>";

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        foreach (var uid in model.Uids)
        {
            var e = model.FindElement(uid);
            _output.WriteLine($"Uid={e!.Uid}, Type={e.ElementType}, SourceUid={e.SourceUid}");
        }

        var aggregator = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        aggregator.Should().NotBeNull();
        aggregator!.SourceUid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var sensor = model.FindElement(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        sensor.Should().NotBeNull();
        sensor!.SourceUid.Should().BeNull();

        var consumers = model.GetConsumersOf(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        consumers.Should().HaveCount(1);
        consumers[0].Uid.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }
}
