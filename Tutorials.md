# MinimalActorSystem Cookbook

Практическое руководство по использованию минималистичной акторной системы. От быстрого старта до продвинутых паттернов.

---

## Содержание

1. [Быстрый старт: минимальное приложение](#1-быстрый-старт-минимальное-приложение)
2. [Описание модели в XML](#2-описание-модели-в-xml)
   - [Простейшая модель](#21-простейшая-модель)
   - [Текстовые свойства](#22-текстовые-свойства)
   - [Группирующие элементы](#23-группирующие-элементы)
   - [Обязательные свойства](#24-обязательные-свойства)
   - [Префиксы чисел](#25-префиксы-чисел)
3. [Создание прикладных акторов](#3-создание-прикладных-акторов)
   - [Базовый актор](#31-базовый-актор)
   - [Пользовательские письма](#32-пользовательские-письма)
   - [Аккумулятор](#33-аккумулятор)
4. [Отправка и обработка сообщений](#4-отправка-и-обработка-сообщений)
5. [Работа с таймаутами](#5-работа-с-таймаутами)
   - [Регистрация и отмена](#51-регистрация-и-отмена)
   - [Таймаут ожидания ответа](#52-таймаут-ожидания-ответа)
6. [Логирование](#6-логирование)
   - [Файловый логгер](#61-файловый-логгер)
   - [Callback-логгер для тестов](#62-callback-логгер-для-тестов)
7. [Тестирование](#7-тестирование)
   - [Синхронный режим](#71-синхронный-режим)
   - [Виртуальное время](#72-виртуальное-время)
   - [Тестовые расширения](#73-тестовые-расширения)
   - [Модульные тесты ElementConfig](#74-модульные-тесты-elementconfig)
   - [Интеграционные тесты XML-компиляции](#75-интеграционные-тесты-xml-компиляции)
8. [Обработка ошибок и Panic](#8-обработка-ошибок-и-panic)
9. [Типовые паттерны](#9-типовые-паттерны)
   - [Request-Response](#91-request-response)
   - [Scatter-Gather](#92-scatter-gather)
   - [Circuit Breaker](#93-circuit-breaker)
   - [Актор-агрегатор](#94-актор-агрегатор)
10. [Структура проекта](#10-структура-проекта)

---

## 1. Быстрый старт: минимальное приложение

### Program.cs

    using Microsoft.Extensions.Logging;
    using MinimalActorSystem;
    using MinimalActorSystem.CompiledModels;

    var settings = new Settings
    {
        IsProduction = false,
        SynchronousProcessing = false
    };

    var system = new ActorSystem(settings);

    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    system.Logger = loggerFactory.CreateLogger("ActorSystem");

    system.TimeService = new SystemTimeService(system);

    var xml = File.ReadAllText("model.xml");
    var compiler = new XmlModelCompiler();
    compiler.AddRule("PingActor", new ElementRule().WithProperties("IntervalMs"));
    compiler.AddRule("PongActor");

    var compiledModel = compiler.Compile(xml);
    var modelActor = new MyAppModelActor(system, compiledModel);

    system.RegisterActor(modelActor);
    modelActor.TryEnqueue(new InitializeLetter(SystemUids.System, SystemUids.Model));

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        system.Shutdown();
    };

    await system.WaitForShutdownAsync();
    return system.IsPanic ? 1 : 0;

### MyAppModelActor.cs

    using MinimalActorSystem;
    using MinimalActorSystem.CompiledModels;

    public class MyAppModelActor : CompiledModelActor
    {
        private readonly CompiledModel _model;

        public MyAppModelActor(IActorSystem system, CompiledModel model) : base(system)
        {
            _model = model;
        }

        protected override CompiledModel CompileModel() => _model;

        protected override object CreateObject(ElementConfig element)
        {
            return element.ElementType switch
            {
                "PingActor" => new PingActor(System, element),
                "PongActor" => new PongActor(System, element),
                _ => throw new InvalidOperationException()
            };
        }

        protected override void OnAfterCreate(CompiledModel model)
        {
            var pingElements = model.FindByType("PingActor");
            var pongElements = model.FindByType("PongActor");
            if (pingElements.Count > 0 && pongElements.Count > 0)
            {
                var ping = GetObject<PingActor>(pingElements[0].Uid);
                var pong = GetObject<PongActor>(pongElements[0].Uid);
                ping.PongUid = pong.Uid;
            }
        }
    }

---

## 2. Описание модели в XML

### 2.1. Простейшая модель

    <Model>
      <PingActor Uid="a0000000-0000-0000-0000-000000000001" Name="ping" IntervalMs="1000" />
      <PongActor Uid="a0000000-0000-0000-0000-000000000002" Name="pong" />
    </Model>

- Каждый элемент обязан иметь Uid (или uid, Id, id)
- Атрибуты становятся свойствами ElementConfig

### 2.2. Текстовые свойства

    <ConfigActor Uid="c0000000-0000-0000-0000-000000000001">
      <Endpoint>https://api.example.com/v2</Endpoint>
      <ApiKey>sk-abc123xyz</ApiKey>
    </ConfigActor>

Результат: свойства Endpoint и ApiKey добавляются в ElementConfig родителя.

### 2.3. Группирующие элементы

    <Model>
      <Actors>
        <Worker Uid="w-0001" Name="worker1" />
        <Worker Uid="w-0002" Name="worker2" />
      </Actors>
      <Connections>
        <Connection Uid="c-0001" From="w-0001" To="w-0002" />
      </Connections>
    </Model>

Правила:

    compiler.AddRule("Actors", new ElementRule().WithGroupElement("Actors"));
    compiler.AddRule("Connections", new ElementRule().WithGroupElement("Connections"));
    compiler.AddRule("Worker", new ElementRule().WithRequired("Name"));
    compiler.AddRule("Connection", new ElementRule().WithRequired("From", "To"));

### 2.4. Обязательные свойства

    compiler.AddRule("Database", new ElementRule()
        .WithRequired("ConnectionString", "MaxPoolSize"));

Если обязательное свойство отсутствует — элемент пропускается с предупреждением.

### 2.5. Префиксы чисел

    <Config Uid="cfg-01" MaxRetries="0b101" BaseAddress="0x1A3F" Mask="0o755" />

Поддерживаются: 0x (hex), 0b (binary), 0o (octal) для int, long, double.

---

## 3. Создание прикладных акторов

### 3.1. Базовый актор

    public class PingActor : Actor
    {
        private readonly int _intervalMs;
        private readonly TimeoutCallback _timerCallback;
        private int _counter;
        public Guid PongUid { get; set; }

        public PingActor(IActorSystem system, ElementConfig config)
            : base(system, config.Uid, config.TryGetString("Name", out var n) ? n : "ping")
        {
            config.TryGetInt32("IntervalMs", out _intervalMs);
            _timerCallback = new TimeoutCallback(Uid, callbackId: 1, OnTimerTick);
        }

        protected override async ValueTask OnLetter(Letter letter)
        {
            if (letter is PongLetter pong)
            {
                System.Logger.LogInformation("Received Pong #{Counter}", pong.Counter);
                ScheduleNextPing();
            }
            await ValueTask.CompletedTask;
        }

        private void ScheduleNextPing()
        {
            System.TimeService.Register(
                TimeSpan.FromMilliseconds(_intervalMs), _timerCallback);
        }

        private void OnTimerTick()
        {
            _counter++;
            System.Send(new PingLetter(Uid, PongUid, _counter));
        }
    }

### 3.2. Пользовательские письма

    public sealed class PingLetter : Letter
    {
        public int Counter { get; }
        public PingLetter(Guid sender, Guid receiver, int counter) 
            : base(sender, receiver) { Counter = counter; }
    }

    public sealed class PongLetter : Letter
    {
        public int Counter { get; }
        public PongLetter(Guid sender, Guid receiver, int counter) 
            : base(sender, receiver) { Counter = counter; }
    }

### 3.3. Аккумулятор

    public sealed class GatherLetter : Letter
    {
        public List<string> Results { get; } = [];
        public int ExpectedCount { get; init; }
        public GatherLetter(Guid sender, Guid receiver, int expectedCount) 
            : base(sender, receiver) { ExpectedCount = expectedCount; }
    }

    // Использование:
    var gather = new GatherLetter(Uid, worker1Uid, expectedCount: 3);
    System.Send(gather);
    gather.Results.Add("worker1 done");
    gather.Sender = Uid;
    gather.Receiver = worker2Uid;
    System.Send(gather);

---

## 4. Отправка и обработка сообщений

    // Прямая отправка
    var letter = new MyLetter(Uid, receiverUid, data);
    system.Send(letter);

    // Отправка самому себе
    system.Send(new MyLetter(Uid, Uid, data));

    // Обработка в акторе
    protected override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case MyLetter msg:
                await ProcessMyLetter(msg);
                break;
        }
    }

---

## 5. Работа с таймаутами

### 5.1. Регистрация и отмена

    public class RetryActor : Actor
    {
        private readonly TimeoutCallback _retryCallback;
        private int _retryCount;

        public RetryActor(IActorSystem system, Guid uid, string name) 
            : base(system, uid, name)
        {
            _retryCallback = new TimeoutCallback(uid, callbackId: 1, OnRetry);
        }

        private void StartRetryLoop()
        {
            _retryCount = 0;
            System.TimeService.Register(
                TimeSpan.FromSeconds(1), _retryCallback);
        }

        private void OnRetry()
        {
            _retryCount++;
            if (_retryCount < 5)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, _retryCount));
                System.TimeService.Register(delay, _retryCallback);
            }
            else
            {
                System.Logger.LogError("Max retries exceeded");
                System.Panic();
            }
        }

        protected override async ValueTask OnShutdown()
        {
            System.TimeService.Unregister(_retryCallback);
            await base.OnShutdown();
        }
    }

### 5.2. Таймаут ожидания ответа

    private TaskCompletionSource<ResponseLetter>? _pendingRequest;
    private readonly TimeoutCallback _timeoutCallback;

    _pendingRequest = new TaskCompletionSource<ResponseLetter>();
    System.TimeService.Register(TimeSpan.FromSeconds(5), _timeoutCallback);

    // При получении ответа:
    System.TimeService.Unregister(_timeoutCallback);
    _pendingRequest?.TrySetResult(response);

    // При срабатывании таймаута:
    private void OnTimeout()
    {
        _pendingRequest?.TrySetException(
            new TimeoutException("Request timed out"));
    }

---

## 6. Логирование

### 6.1. Файловый логгер

    using MinimalActorSystem;

    var system = new ActorSystem(settings);
    
    // Создать файловый логгер одной строкой
    system.CreateFileLogger("myapp", minLevel: LogLevel.Information);
    
    // Файл создаётся в директории Logs/myapp.log
    // Директория создаётся автоматически

Настройка директории логов:

    ActorSystemExtensions.FileLoggerDirectory = "/var/log/myapp";

Особенности:
- Сообщения буферизуются в памяти и сбрасываются на диск пачками с задержкой до 100 мс
- Потокобезопасен
- При завершении приложения вызовите Dispose у логгера для сброса оставшихся сообщений

Формат строки лога:

    01.02.2024 12:34:56.789 [Information] [7] Actor ping: message text

### 6.2. Callback-логгер для тестов

    system.CreateTestLogger(line =>
    {
        _testOutputHelper.WriteLine(line);
    });

Сообщения передаются в делегат. Удобен для проверки логов в тестах через Assert.

---

## 7. Тестирование

### 7.1. Синхронный режим

    [Test]
    public void PingActor_SendsPing_OnStartup()
    {
        var settings = new Settings
        {
            SynchronousProcessing = true,
            IsProduction = false
        };
        var system = new ActorSystem(settings);
        var ping = new PingActor(system, testUid, "test-ping");
        system.RegisterActor(ping);
        system.Send(new InitializeLetter(SystemUids.System, testUid));
        Assert.That(ping.Counter, Is.EqualTo(1));
    }

### 7.2. Виртуальное время

    public class VirtualTimeService : ITimeService
    {
        private DateTime _currentTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly SortedList<DateTime, List<TimeoutCallback>> _timers = [];

        public DateTime UtcNow => _currentTime;

        public void Register(DateTime deadline, TimeoutCallback callback)
        {
            if (!_timers.ContainsKey(deadline))
                _timers[deadline] = [];
            _timers[deadline].Add(callback);
        }

        public void Register(TimeSpan timeout, TimeoutCallback callback)
            => Register(_currentTime + timeout, callback);

        public void Unregister(TimeoutCallback callback)
        {
            foreach (var list in _timers.Values)
                list.RemoveAll(c => c.Equals(callback));
        }

        public void Advance(TimeSpan time)
        {
            var targetTime = _currentTime + time;
            while (_timers.Count > 0 && _timers.Keys[0] <= targetTime)
            {
                _currentTime = _timers.Keys[0];
                var callbacks = _timers[_currentTime];
                _timers.RemoveAt(0);
                foreach (var cb in callbacks)
                    cb.Action();
            }
            _currentTime = targetTime;
        }
    }

### 7.3. Тестовые расширения

    // Файловый логгер для теста (имя файла = имя тестового метода)
    system.CreateTestFileLogger();
    // Создаст Logs/MyTestMethod.log

    // Callback-логгер для проверки в Assert
    var logLines = new List<string>();
    system.CreateTestLogger(line => logLines.Add(line));
    // ... выполнение теста ...
    Assert.That(logLines, Has.Some.Contain("expected message"));

    // Настройка директории для тестовых логов
    ActorSystemTestExtensions.TestFileLoggerDirectory = @"C:\TestLogs";

### 7.4. Модульные тесты ElementConfig

    [Test]
    public void TryGetInt32_ParsesHexPrefix()
    {
        var config = new ElementConfig();
        config.AddProperty("Value", "0xFF");
        Assert.That(config.TryGetInt32("Value", out var value), Is.True);
        Assert.That(value, Is.EqualTo(255));
    }

    [Test]
    public void TryGetInt32_ParsesBinaryPrefix()
    {
        var config = new ElementConfig();
        config.AddProperty("Mask", "0b1010");
        Assert.That(config.TryGetInt32("Mask", out var value), Is.True);
        Assert.That(value, Is.EqualTo(10));
    }

### 7.5. Интеграционные тесты XML-компиляции

    [Test]
    public void Compile_ValidXml_ReturnsCorrectModel()
    {
        var xml = """
            <Model>
              <Worker Uid="00000000-0000-0000-0000-000000000001" Name="w1" />
              <Worker Uid="00000000-0000-0000-0000-000000000002" Name="w2" />
            </Model>
            """;

        var compiler = new XmlModelCompiler();
        var model = compiler.Compile(xml);

        Assert.That(model.Count, Is.EqualTo(2));
        Assert.That(compiler.Warnings, Is.Empty);
    }

    [Test]
    public void Compile_MissingRequiredProperty_AddsWarning()
    {
        var xml = """
            <Model>
              <Database Uid="00000000-0000-0000-0000-000000000001" />
            </Model>
            """;

        var compiler = new XmlModelCompiler();
        compiler.AddRule("Database", 
            new ElementRule().WithRequired("ConnectionString"));
        var model = compiler.Compile(xml);

        Assert.That(model.Count, Is.EqualTo(0));
        Assert.That(compiler.Warnings, Has.Count.EqualTo(1));
    }

---

## 8. Обработка ошибок и Panic

### Аварийное завершение

    try
    {
        var result = await _externalService.DoWork(data);
        if (!result.Success)
            throw new InvalidOperationException("Service failure");
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        System.Logger.LogCritical(ex, "Critical failure");
        System.Panic();
    }

### Проверка причины завершения

    await system.WaitForShutdownAsync();
    Environment.Exit(system.IsPanic ? 1 : 0);

---

## 9. Типовые паттерны

### 9.1. Request-Response

    public sealed class QueryLetter : Letter
    {
        public string Sql { get; init; }
        public QueryLetter(Guid s, Guid r, string sql) : base(s, r) { Sql = sql; }
    }

    public sealed class QueryResultLetter : Letter
    {
        public object? Result { get; init; }
        public QueryResultLetter(Guid s, Guid r, object? result) 
            : base(s, r) { Result = result; }
    }

### 9.2. Scatter-Gather

    public sealed class ScatterGatherLetter : Letter
    {
        public List<string> Results { get; } = [];
        public int ExpectedCount { get; init; }
        public TaskCompletionSource<List<string>> Completion { get; init; }

        public ScatterGatherLetter(Guid sender, Guid receiver, int expectedCount) 
            : base(sender, receiver)
        {
            ExpectedCount = expectedCount;
            Completion = new TaskCompletionSource<List<string>>();
        }
    }

### 9.3. Circuit Breaker

    private enum State { Closed, Open, HalfOpen }

    if (_failureCount >= FailureThreshold)
    {
        _state = State.Open;
        System.TimeService.Register(ResetTimeout, _resetCallback);
    }

### 9.4. Актор-агрегатор

    public class StateAggregatorActor : Actor
    {
        private readonly Dictionary<string, double> _metrics = [];

        protected override async ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case MetricUpdateLetter update:
                    _metrics[update.Key] = update.Value;
                    break;
                case GetMetricsLetter get:
                    var snapshot = new Dictionary<string, double>(_metrics);
                    System.Send(
                        new MetricsSnapshotLetter(Uid, get.Sender, snapshot));
                    break;
            }
        }
    }

---

## 10. Структура проекта

    MyApp/
    ├── Actors/
    │   ├── MyAppModelActor.cs
    │   ├── PingActor.cs
    │   └── ...
    ├── Letters/
    │   ├── PingLetter.cs
    │   └── ...
    ├── model.xml
    ├── Program.cs
    └── MyApp.csproj

---

