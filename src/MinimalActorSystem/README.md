# MinimalActorSystem

Минималистичная акторная система на C#. Акторы изолированы, общаются только через сообщения, не разделяют изменяемое состояние и не блокируют потоки в ожидании.

## Принципы

- **Ничего лишнего.** Каждый элемент имеет однозначную причину существования.
- **Изоляция.** Единственный способ взаимодействия — асинхронный обмен sealed-письмами.
- **Последовательная обработка.** Один актор — одно сообщение в момент времени.
- **Никакого разделяемого состояния.** Нет локов, нет гонок.
- **Виртуальное время.** Тесты с длительными таймаутами выполняются мгновенно.


Единственная зависимость: `Microsoft.Extensions.Logging.Abstractions`.

## Быстрый старт

    using MinimalActorSystem;
    using Microsoft.Extensions.Logging;

    // 1. Настройки
    var settings = new Settings { TimeServiceModes = TimeServiceModes.System };

    // 2. Акторная система
    var system = new ActorSystem(settings);

    // 3. Логгер
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    system.Logger = loggerFactory.CreateLogger("ActorSystem");

    // 4. Реальное время
    system.TimeService = new SystemTimeService(system);

    // 5. Модельный актор
    var model = new MyModelActor(system);
    system.RegisterActor(model);
    model.SendInitialize();

    // 6. Завершение по сигналу
    Console.ReadLine();
    system.Shutdown();
    await system.WaitForShutdownAsync();

## Основные понятия

### Актор

Владеет состоянием, ни с кем его не разделяет. Имеет `Guid Uid`, имя и очередь входящих сообщений. Обрабатывает сообщения строго по одному. Когда сообщений нет — не потребляет процессорное время.

    public class MyActor : Actor
    {
        public MyActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name) { }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case MyMessage msg:
                    // Обработка сообщения
                    break;
            }
            return default;
        }
    }

### Письмо (Letter)

Сообщение, которым обмениваются акторы. Все письма наследуются от `Letter` и обязаны быть `sealed`. Содержат `Sender` и `Receiver` — идентификаторы отправителя и получателя.

    public sealed class Ping : Letter
    {
        public int Remaining { get; }
        public Ping(Guid sender, Guid receiver, int remaining)
            : base(sender, receiver)
        {
            Remaining = remaining;
        }
    }

### Акторная система

Экземпляр `ActorSystem` управляет реестром акторов, доставляет письма, предоставляет логгер, сервис времени и настройки. В одном приложении может быть несколько независимых акторных систем.

### Модельный актор

Корневой родитель всех прикладных акторов. Получает `InitializeLetter` и создаёт прикладных акторов. Может использовать императивный (`ModelActor`) или декларативный (`CompiledModelActor`) способ построения.

**Императивное построение:**

    public class MyModel : ModelActor
    {
        public MyModel(IActorSystem system) : base(system) { }

        protected override void OnBuildModel()
        {
            var actor = new MyActor(System, Guid.NewGuid(), "worker");
            Create(actor);
        }
    }

**Декларативное построение (XML):**

    public class MyCompiledModel : CompiledModelActor
    {
        public MyCompiledModel(IActorSystem system) : base(system) { }

        protected override CompiledModel CompileModel()
        {
            var compiler = new XmlModelCompiler("Uid")
                .AddRule("Worker", ElementRule.Default
                    .WithRequired("Uid")
                    .WithProperties("Name"));

            return compiler.Compile(xmlString);
        }

        protected override object CreateObject(ElementConfig element)
        {
            return element.ElementType switch
            {
                "Worker" => new MyActor(System, element.Uid, 
                    element.TryGetString("Name", out var n) ? n : "worker"),
                _ => throw new InvalidOperationException()
            };
        }
    }

## Таймауты

Акторы регистрируют таймауты через сервис времени. Таймаут — это коллбек, который срабатывает в заданный момент и доставляется актору как `TimeServiceLetter`.

    public class ReconnectingActor : Actor
    {
        private readonly TimeoutCallback _reconnectTimeout;

        public ReconnectingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            _reconnectTimeout = new TimeoutCallback(Uid, callbackId: 1, OnReconnectTimeout);
        }

        private void OnReconnectTimeout()
        {
            System.Logger.LogWarning("{Name}: таймаут подключения", Name);
            TryConnect();
        }

        private void TryConnect()
        {
            // Попытка подключения...
            if (!_connected)
                System.TimeService.Register(TimeSpan.FromSeconds(5), _reconnectTimeout);
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeout)
                timeout.Callback.Action();
            return default;
        }
    }

## Тестирование с виртуальным временем

Тесты с длительными временными интервалами выполняются мгновенно, без реальных задержек.

**Синхронный режим (рекомендуется):**

    var settings = new Settings { TimeServiceModes = TimeServiceModes.Sync };
    var system = new ActorSystem(settings);
    system.Logger = new CallbackLogger(msg => TestContext.WriteLine(msg));

    var timeService = new VirtualTimeService(system);
    timeService.SetTime(DateTime.UtcNow);
    system.TimeService = timeService;

    // Построение модели
    var model = new PingPongModel(system);
    system.RegisterActor(model);
    model.SendInitialize();

    // Продвижение виртуального времени
    timeService.StartVirtualClock(startTime, endTime, TimeSpan.FromSeconds(1));

    // Проверки...

**Имитация внешней среды:**

    public class TelemetrySimulator : IVirtualTimeSubscriber
    {
        private readonly IActorSystem _system;
        private readonly Guid _target;
        public string Name => "TelemetrySimulator";

        public TelemetrySimulator(IActorSystem system, Guid target)
        {
            _system = system;
            _target = target;
        }

        public ValueTask OnTimeStep(DateTime currentTime)
        {
            if (currentTime.Second % 10 == 0)
                _system.Send(new TelemetryLetter(SystemUids.System, _target, /*...*/));
            return default;
        }
    }

    timeService.Subscribe(new TelemetrySimulator(system, someActor.Uid));

## Производительность

Результаты бенчмарка на 6000 акторов и 30 миллионов сообщений:

- **2,3 млн сообщений/сек** — при тысячах активных акторов
- **0,43 мкс на сообщение** — включая доставку и обработку
- **52 мс на создание 6000 акторов** — создание практически бесплатно
- **~6 КБ памяти на актор** — с очередью в 256 сообщений
- **0 потерянных сообщений** — переполнения очереди не было

## Паттерны и приёмы

Документация включает практические паттерны (см. `TipsAndTricks.md`):

- **Circuit Breaker** — защита от падения внешних сервисов
- **Рандеву с таймаутом** — ожидание ответа от другого актора
- **Аккумулятор** — перемещаемая изменяемая посылка
- **Маршрутизатор** — распределение писем по критерию
- **Сторож** — контроль здоровья дочернего актора
- **Отложенная очередь** — письма на будущее

## Обработка ошибок

Исключения в `OnLetter` логируются и не прерывают цикл обработки. Если ошибка делает дальнейшую работу актора невозможной — вызывается `System.Panic()`, что приводит к кооперативному завершению всех акторов.

Штатное завершение — `System.Shutdown()` с последующим `await system.WaitForShutdownAsync()`. Внешний код проверяет `system.IsPanic` для определения кода возврата.

## Лицензия

MIT
