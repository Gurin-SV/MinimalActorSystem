# MinimalActorSystem: Туториал и Быстрый Старт

MinimalActorSystem — это минималистичная акторная система на C#, спроектированная для приложений, которым нужна лёгкая конкурентность без тяжёлых фреймворков. Акторы изолированы, общаются только через сообщения, не разделяют изменяемое состояние и не блокируют потоки в ожидании. Система не требует внешних зависимостей, кроме стандартной библиотеки .NET и Microsoft.Extensions.Logging.

Этот туториал проведёт вас от установки до написания первого актора и теста с виртуальным временем. Предполагается, что вы уже знакомы с C# и async/await.

## Быстрый старт

Создайте консольное приложение .NET и добавьте ссылку на сборку MinimalActorSystem. Если вы клонировали репозиторий, просто добавьте проект в решение.

Минимальная программа, которая запускает акторную систему с одним актором, выглядит так:

    using MinimalActorSystem;
    using Microsoft.Extensions.Logging;

    // 1. Создаём настройки
    var settings = new Settings
    {
        TimeServiceModes = TimeServiceModes.System
    };

    // 2. Создаём акторную систему
    var system = new ActorSystem(settings);

    // 3. Подключаем логгер
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    system.Logger = loggerFactory.CreateLogger("ActorSystem");

    // 4. Подключаем сервис времени
    system.TimeService = new SystemTimeService(system);

    // 5. Создаём модельный актор
    var model = new MyModelActor(system);
    system.RegisterActor(model);

    // 6. Запускаем построение модели
    model.SendInitialize();

    // 7. Ждём завершения (по Ctrl+C или другому сигналу)
    Console.WriteLine("Нажмите Enter для завершения...");
    Console.ReadLine();

    // 8. Штатное завершение
    system.Shutdown();
    await system.WaitForShutdownAsync();

Метод `SendInitialize` — это метод расширения, который отправляет `InitializeLetter` модельному актору. Вы можете написать его сами:

    public static void SendInitialize(this Actor actor)
    {
        var letter = new InitializeLetter(actor.Uid, actor.Uid);
        actor.System.Send(letter);
    }

После вызова `SendInitialize` модельный актор построит прикладных акторов, и система готова к работе.

## Основные понятия

**Актор** — это объект, который владеет своим состоянием и ни с кем его не разделяет. У актора есть уникальный идентификатор (`Guid Uid`), имя для диагностики и очередь входящих сообщений. Актор обрабатывает сообщения строго по одному за раз. Когда сообщений нет, актор не потребляет процессорное время.

**Письмо (Letter)** — это сообщение, которым обмениваются акторы. Все письма наследуются от абстрактного класса `Letter` и обязаны быть `sealed`. Письмо содержит `Sender` и `Receiver` — идентификаторы отправителя и получателя.

**Акторная система** — экземпляр класса `ActorSystem`, который управляет реестром акторов, доставляет письма, предоставляет логгер, сервис времени и настройки. В одном приложении может быть несколько независимых акторных систем.

**Модельный актор** — корневой родитель всех прикладных акторов. Он получает `InitializeLetter` и в ответ создаёт прикладных акторов. В системе может быть только один модельный актор, его идентификатор — `SystemUids.Model`.

## Пишем первый актор: Пинг-Понг

Классический пример — два актора, которые перебрасываются сообщениями заданное число раз.

Сначала определим письма. Письмо `Ping` идёт от одного актора к другому, `Pong` — обратно. Оба содержат счётчик оставшихся обменов:

    public sealed class Ping : Letter
    {
        public int Remaining { get; }
        public Ping(Guid sender, Guid receiver, int remaining)
            : base(sender, receiver)
        {
            Remaining = remaining;
        }
    }

    public sealed class Pong : Letter
    {
        public int Remaining { get; }
        public Pong(Guid sender, Guid receiver, int remaining)
            : base(sender, receiver)
        {
            Remaining = remaining;
        }
    }

Теперь актор, который умеет принимать `Ping` и отвечать `Pong`:

    public class PingActor : Actor
    {
        public PingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name) { }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case Ping ping:
                    System.Logger.LogInformation("{Name}: получил Ping, осталось {Remaining}",
                        Name, ping.Remaining);

                    if (ping.Remaining > 0)
                    {
                        var pong = new Pong(Uid, ping.Sender, ping.Remaining - 1);
                        System.Send(pong);
                    }
                    else
                    {
                        System.Logger.LogInformation("{Name}: игра окончена", Name);
                    }
                    break;

                case Pong pong:
                    System.Logger.LogInformation("{Name}: получил Pong, осталось {Remaining}",
                        Name, pong.Remaining);

                    if (pong.Remaining > 0)
                    {
                        var ping = new Ping(Uid, pong.Sender, pong.Remaining - 1);
                        System.Send(ping);
                    }
                    else
                    {
                        System.Logger.LogInformation("{Name}: игра окончена", Name);
                    }
                    break;
            }
            return default;
        }
    }

Модельный актор создаёт двух игроков и отправляет первый `Ping`:

    public class PingPongModel : ModelActor
    {
        public PingPongModel(IActorSystem system) : base(system) { }

        protected override void OnBuildModel()
        {
            var alice = new PingActor(System, Guid.NewGuid(), "Alice");
            var bob = new PingActor(System, Guid.NewGuid(), "Bob");

            Create(alice);
            Create(bob);

            // Отправляем первый Ping: Alice -> Bob, 5 обменов
            var firstPing = new Ping(alice.Uid, bob.Uid, 5);
            System.Send(firstPing);
        }
    }

Обратите внимание: первый `Ping` отправляется после того, как оба актора созданы через `Create`. Метод `Create` немедленно регистрирует актора в системе и запускает его цикл обработки, поэтому к моменту отправки оба уже активны и готовы принимать письма.

## Таймауты

Акторы могут регистрировать таймауты через сервис времени. Таймаут — это коллбек, который срабатывает в заданный момент времени и доставляется актору в виде `TimeServiceLetter`.

Для работы с таймаутами актор создаёт `TimeoutCallback` в конструкторе и регистрирует его через `System.TimeService.Register`. Когда таймаут сработает, актор получит `TimeServiceLetter` и вызовет `callback.Action()`.

Пример актора с таймаутом переподключения:

    public class ReconnectingActor : Actor
    {
        private readonly TimeoutCallback _reconnectTimeout;
        private bool _connected;

        public ReconnectingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            _reconnectTimeout = new TimeoutCallback(Uid, callbackId: 1, OnReconnectTimeout);
        }

        private void OnReconnectTimeout()
        {
            System.Logger.LogWarning("{Name}: таймаут подключения, пробуем снова", Name);
            TryConnect();
        }

        private void TryConnect()
        {
            // Пытаемся подключиться...
            _connected = true;

            // Если не получилось — регистрируем таймаут на повторную попытку
            if (!_connected)
            {
                System.TimeService.Register(TimeSpan.FromSeconds(5), _reconnectTimeout);
            }
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case TimeServiceLetter timeout:
                    timeout.Callback.Action();
                    break;
            }
            return default;
        }
    }

Важно: коллбек таймаута не должен взаимодействовать с другими акторами. Его единственная задача — выполнить небольшое действие внутри актора-владельца.

## Тестирование с виртуальным временем

Главное преимущество MinimalActorSystem — возможность тестировать сценарии с длительными временными интервалами мгновенно, без реальных задержек.

Для тестов используется `VirtualTimeService` и настройка `TimeServiceModes.Sync` (синхронная обработка писем) или `TimeServiceModes.Async` (асинхронная с ожиданием).

### Синхронный режим тестирования

Синхронный режим — самый простой. Письма обрабатываются немедленно в потоке вызывающего кода, таймауты срабатывают при продвижении виртуального времени.

    using MinimalActorSystem;
    using MinimalActorSystem.Testing;

    [Test]
    public void PingPong_CompletesInFiveExchanges()
    {
        // 1. Настройка
        var settings = new Settings { TimeServiceModes = TimeServiceModes.Sync };
        var system = new ActorSystem(settings);

        // Логгер для отладки
        system.CreateTestLogger(TestContext.WriteLine);

        // Виртуальное время
        var timeService = new VirtualTimeService(system);
        timeService.SetTime("01.01.2024 12:00:00".AsUtc());
        system.TimeService = timeService;

        // 2. Модель
        var model = new PingPongModel(system);
        system.RegisterActor(model);
        model.SendInitialize();

        // 3. Запуск виртуальных часов (если нужно гонять время)
        timeService.StartVirtualClock(
            startTime: "01.01.2024 12:00:00".AsUtc(),
            endTime: "01.01.2024 12:01:00".AsUtc(),
            step: TimeSpan.FromSeconds(1)
        );

        // 4. Проверки...
    }

`StartVirtualClock` проходит от `startTime` до `endTime` с шагом `step`. На каждом шаге применяются отложенные операции регистрации/отмены таймаутов, срабатывают истёкшие таймауты, и вызываются подписчики виртуального времени (о них ниже). В синхронном режиме все письма обрабатываются сразу же.

### Асинхронный режим тестирования

Асинхронный режим использует `StartVirtualClockAsync` и `TimeServiceModes.Async`. После каждого шага виртуального времени система ждёт, пока все акторы обработают свои очереди (`WaitAllIdleAsync`). Это позволяет тестировать сценарии, где порядок обработки писем важен.

    [Test]
    public async Task PingPongAsync_Completes()
    {
        var settings = new Settings { TimeServiceModes = TimeServiceModes.Async };
        var system = new ActorSystem(settings);
        system.CreateTestLogger(TestContext.WriteLine);

        var timeService = new VirtualTimeService(system);
        timeService.SetTime("01.01.2024 12:00:00".AsUtc());
        system.TimeService = timeService;

        var model = new PingPongModel(system);
        system.RegisterActor(model);
        model.SendInitialize();

        await timeService.StartVirtualClockAsync(
            startTime: "01.01.2024 12:00:00".AsUtc(),
            endTime: "01.01.2024 12:01:00".AsUtc(),
            step: TimeSpan.FromSeconds(1)
        );
    }

### Имитаторы внешней среды

Часто в тестах нужно имитировать внешнюю среду: источники данных, сбои соединений, изменение конфигурации. Для этого используется интерфейс `IVirtualTimeSubscriber`.

Подписчик получает управление на каждом шаге виртуального времени и может отправить письмо актору, имитируя внешнее событие:

    public class TelemetrySimulator : IVirtualTimeSubscriber
    {
        private readonly IActorSystem _system;
        private readonly Guid _targetActor;

        public string Name => "TelemetrySimulator";

        public TelemetrySimulator(IActorSystem system, Guid targetActor)
        {
            _system = system;
            _targetActor = targetActor;
        }

        public ValueTask OnTimeStep(DateTime currentTime)
        {
            // Имитируем поступление телеметрии каждые 10 секунд
            if (currentTime.Second % 10 == 0)
            {
                var data = new TelemetryData(/* ... */);
                _system.Send(new TelemetryLetter(
                    sender: SystemUids.System,
                    receiver: _targetActor,
                    data: data
                ));
            }
            return default;
        }
    }

Подписчик регистрируется в `VirtualTimeService`:

    var simulator = new TelemetrySimulator(system, someActor.Uid);
    timeService.Subscribe(simulator);

Теперь при каждом вызове `StartVirtualClock` или `StartVirtualClockAsync` имитатор будет получать управление на каждом шаге и отправлять письма в соответствии с модельным временем.

## Декларативное построение модели из XML

Если прикладная система содержит много акторов со сложными связями, вместо императивного создания в `ModelActor.OnBuildModel` можно использовать `CompiledModelActor` и XML-описание.

XML-описание модели:

    <Model>
      <Sensor Uid="a1b2c3d4-...">
        <Name>TemperatureSensor</Name>
        <Interval>0x3E8</Interval>
      </Sensor>
      <Controller Uid="e5f6a7b8-...">
        <SetPoint>22.5</SetPoint>
        <Deadband>0.5</Deadband>
      </Controller>
    </Model>

Компиляция и использование:

    public class MyCompiledModelActor : CompiledModelActor
    {
        public MyCompiledModelActor(IActorSystem system) : base(system) { }

        protected override CompiledModel CompileModel()
        {
            var compiler = new XmlModelCompiler("Uid")
                .AddRule("Sensor", new ElementRule()
                    .WithRequired("Uid")
                    .WithProperties("Name", "Interval"))
                .AddRule("Controller", new ElementRule()
                    .WithRequired("Uid")
                    .WithProperties("SetPoint", "Deadband"));

            var xml = File.ReadAllText("model.xml");
            return compiler.Compile(xml);
        }

        protected override object CreateObject(ElementConfig element)
        {
            return element.ElementType switch
            {
                "Sensor" => CreateSensor(element),
                "Controller" => CreateController(element),
                _ => throw new InvalidOperationException($"Unknown element type: {element.ElementType}")
            };
        }

        private SensorActor CreateSensor(ElementConfig element)
        {
            element.TryGetString("Name", out var name);
            element.TryGetInt32("Interval", out var interval);
            return new SensorActor(System, element.Uid, name, interval);
        }

        private ControllerActor CreateController(ElementConfig element)
        {
            element.TryGetDouble("SetPoint", out var setPoint);
            element.TryGetDouble("Deadband", out var deadband);
            return new ControllerActor(System, element.Uid, setPoint, deadband);
        }

        protected override void OnAfterCreate(CompiledModel model)
        {
            // Устанавливаем связи между акторами
            var sensor = GetObject<SensorActor>(/* uid датчика */);
            var controller = GetObject<ControllerActor>(/* uid контроллера */);
            sensor.SetController(controller);
        }
    }

Метод `OnAfterCreate` вызывается после создания всех объектов, но до их регистрации в системе. Здесь нельзя отправлять письма — получатели ещё не зарегистрированы. Но можно устанавливать прямые ссылки между акторами, если это нужно для конфигурации.

## Обработка ошибок и завершение

Актор должен перехватывать исключения внутри `OnLetter`. Необработанное исключение логируется, и актор продолжает работу со следующим письмом.

Если ошибка делает дальнейшую работу актора невозможной, он вызывает `System.Panic()`. Это устанавливает флаг `IsPanic` и отменяет токен отмены, что приводит к завершению всех акторов. Внешний код, ожидающий через `WaitForShutdownAsync`, может проверить `system.IsPanic` и завершить процесс с ненулевым кодом возврата.

Штатное завершение запускается вызовом `System.Shutdown()`. Акторы вызывают `OnShutdown`, освобождают ресурсы и удаляются из реестра.

## Резюме

MinimalActorSystem даёт вам:

- Изолированных акторов с последовательной обработкой сообщений
- Асинхронную доставку писем без блокировок
- Сервис времени с поддержкой таймаутов
- Виртуальное время для быстрых детерминированных тестов
- Имитаторы внешней среды через `IVirtualTimeSubscriber`
- Декларативное построение модели из XML для сложных конфигураций
- Встроенное файловое и консольное логирование

Система остается минималистичной: никаких супервизоров, иерархий наследования сообщений, горячей замены кода или распределённых транзакций. Только акторы, письма, очереди и время.
