# Контракты минималистичной акторной системы

Документ дополняет Манифест. Содержит сигнатуры всех сущностей, их отношения и уточняющие комментарии. Детали реализации опущены.

---

## 1. Письмо (Letter)

    public abstract class Letter
    {
        public Guid Sender { get; set; }
        public Guid Receiver { get; set; }

        protected Letter(Guid sender, Guid receiver);
    }

Комментарий: поля мутабельны для случая, когда то же письмо служит ответом — отправитель меняет Sender/Receiver местами и отправляет обратно (п. 6 Манифеста: аккумулятор). Все наследники — sealed (п. 12).

---

## 2. Системные письма

    public sealed class TimeServiceLetter : Letter
    {
        public TimeoutCallback Callback { get; }

        public TimeServiceLetter(Guid sender, Guid receiver, TimeoutCallback callback);
    }

    public sealed class ShutdownLetter : Letter
    {
        public ShutdownLetter(Guid sender, Guid receiver);
    }

    public sealed class InitializeLetter : Letter
    {
        public InitializeLetter(Guid sender, Guid receiver);
    }

Комментарий: TimeServiceLetter — уведомление о сработавшем таймауте (п. 22). Отправитель — SystemUids.TimeService. ShutdownLetter — запрос на завершение конкретного актора. InitializeLetter — сигнал модельному актору на начало построения модели.

---

## 3. Идентификаторы системных акторов

    public static class SystemUids
    {
        public static readonly Guid System;
        public static readonly Guid TimeService;
        public static readonly Guid Model;
    }

Комментарий: статические константные идентификаторы с фиксированными значениями. Доступны напрямую через SystemUids.System и т.д. System — системный идентификатор. TimeService — отправитель TimeServiceLetter. Model — идентификатор модельного актора (ModelActor или CompiledModelActor).

---

## 4. Таймаут и сервис времени

    public readonly struct TimeoutCallback : IEquatable<TimeoutCallback>
    {
        public Guid ActorUid { get; }
        public int CallbackId { get; }
        public Action Action { get; }

        public TimeoutCallback(Guid actorUid, int callbackId, Action action);
    }

    public interface ITimeService
    {
        DateTime UtcNow { get; }

        // Регистрирует коллбек на указанный deadline.
        // Если коллбек с таким (ActorUid, CallbackId) уже зарегистрирован —
        // старая регистрация заменяется новой.
        void Register(DateTime deadline, TimeoutCallback callback);
        void Register(TimeSpan timeout, TimeoutCallback callback);

        // Удаляет регистрацию коллбека. Если коллбек не найден — ничего не делает.
        void Unregister(TimeoutCallback callback);
    }

Комментарий: TimeoutCallback создаётся актором один раз в конструкторе. Один и тот же экземпляр может регистрироваться многократно с разными дедлайнами. CallbackId уникален в рамках одного актора, назначается актором. Ключ в таблице таймаутов — (ActorUid, CallbackId). Unregister удаляет запись полностью — и из таблицы, и из очереди ожидания. TimeServiceLetter доставляет коллбек актору-получателю; обработка состоит в вызове callback.Action().

Реализации:
- `SystemTimeService` — продакшен-реализация с реальным системным временем, использует фоновый цикл для отслеживания таймаутов;
- `VirtualTimeService` — тестовая реализация с виртуальным временем, поддерживает подписчиков (`IVirtualTimeSubscriber`) для имитации внешней среды.

---

## 5. Актор

    public abstract class Actor
    {
        public Guid Uid { get; }
        public string Name { get; }
        public IActorSystem System { get; }
        public int QueueCapacity { get; }

        protected Actor(IActorSystem system, Guid uid, string name, int queueCapacity = DefaultQueueCapacity);
    }

Внутренние члены (доступны только ActorSystem):

    internal bool TryEnqueue(Letter letter);
    internal async Task RunAsync(CancellationToken ct);
    internal void HandleSynchronously(Letter letter);

Защищённые члены (переопределяются наследниками):

    protected abstract ValueTask OnLetter(Letter letter);
    protected virtual ValueTask OnShutdown();

Комментарий: Uid — универсально уникальный идентификатор. Name — имя актора для диагностики. System — единственная ссылка на внешний мир, через неё доступны Logger, TimeService, Settings. Очередь писем встроена в актор (bounded, размер задаётся в конструкторе). При переполнении очереди письма молча отбрасываются.

Цикл обработки (RunAsync) последовательно читает письма из очереди и вызывает OnLetter. При отмене CancellationToken вызывается OnShutdown, затем актор удаляет себя из реестра. Исключения в OnLetter логируются и не прерывают цикл.

HandleSynchronously используется в синхронном режиме (`TimeServiceModes.Sync`) — вызывает OnLetter непосредственно в потоке отправителя, без прохождения через очередь. Если OnLetter возвращает незавершённый Task, блокирует поток отправителя до завершения.

TryEnqueue пытается поместить письмо в очередь. При `TimeServiceModes.Async` вызывает `System.IncrementActivity()` для отслеживания незавершённой активности.

---

## 6. Модельный актор (императивное построение)

    public abstract class ModelActor : Actor
    {
        // Всегда имеет Uid = SystemUids.Model, Name = "model"
        protected ModelActor(IActorSystem system, int queueCapacity = 256);

        // Запечатанный OnLetter — обрабатывает InitializeLetter, остальное делегирует
        protected sealed override ValueTask OnLetter(Letter letter);

        // Вызывается при получении InitializeLetter. Синхронный метод.
        // Наследник создаёт прикладных акторов через Create().
        // При ошибке вызывается System.Panic().
        protected abstract void OnBuildModel();

        // Обрабатывает все письма, кроме InitializeLetter.
        // По умолчанию игнорирует (возвращает default).
        protected virtual ValueTask OnModelLetter(Letter letter);

        // Регистрирует дочернего актора и немедленно запускает его цикл обработки.
        // Должен вызываться только из OnBuildModel.
        protected Guid Create(Actor actor);
    }

Комментарий: ModelActor — промежуточное звено между системными и прикладными акторами (п. 15 Манифеста). Имеет константный Uid (SystemUids.Model) и имя "model". OnLetter запечатан (sealed override): при получении InitializeLetter вызывает OnBuildModel, при ошибке логирует и вызывает System.Panic(). Все остальные письма делегируются в OnModelLetter.

Create регистрирует дочернего актора в системе и немедленно запускает его RunAsync. Актор начинает обрабатывать сообщения сразу после регистрации. Возвращает Guid созданного актора.

---

## 7. Модельный актор (декларативное построение)

    public abstract class CompiledModelActor : Actor
    {
        // Всегда имеет Uid = SystemUids.Model, Name = "model"
        protected CompiledModelActor(IActorSystem system, int queueCapacity = 256);

        // Запечатанный OnLetter — обрабатывает InitializeLetter, остальное делегирует
        protected sealed override ValueTask OnLetter(Letter letter);

        // Компилирует модель (из XML или другого источника)
        protected abstract CompiledModel CompileModel();

        // Создаёт объект по конфигурации элемента
        protected abstract object CreateObject(ElementConfig element);

        // Вызывается после создания всех объектов, до регистрации акторов.
        // Наследник устанавливает связи между объектами.
        protected virtual void OnAfterCreate(CompiledModel model);

        // Обрабатывает все письма, кроме InitializeLetter.
        // По умолчанию игнорирует (возвращает default).
        protected virtual ValueTask OnModelLetter(Letter letter);

        // Получает ранее созданный объект по идентификатору
        protected T GetObject<T>(Guid uid) where T : class;

        // Проверяет, был ли создан объект с указанным идентификатором
        protected bool HasObject(Guid uid);
    }

Комментарий: CompiledModelActor — альтернативный способ построения модели, декларативный. Также имеет Uid = SystemUids.Model и имя "model". Жизненный цикл построения: получение InitializeLetter → компиляция модели через CompileModel() → создание объектов через CreateObject() для каждого элемента → вызов OnAfterCreate() для установки связей → регистрация всех акторов в системе. При ошибке на любом этапе вызывает System.Panic().

Временный словарь `_objects` (Guid → object) хранит все созданные объекты до регистрации акторов. После сборки модели акторы регистрируются и начинают работу.

---

## 8. CompiledModel и вспомогательные классы

    public class CompiledModel
    {
        public IReadOnlyCollection<Guid> Uids { get; }
        public int Count { get; }

        public void Add(ElementConfig element);
        public ElementConfig? FindElement(Guid uid);
        public IReadOnlyList<ElementConfig> FindByType(string elementType);
    }

    public class ElementConfig
    {
        public Guid Uid { get; init; }
        public string ElementType { get; init; }
        public int PropertyCount { get; }
        public IReadOnlyCollection<string> PropertyNames { get; }

        public void AddProperty(string name, string value);
        public bool HasProperty(string name);
        public bool TryGetString(string name, out string value);
        public bool TryGetInt32(string name, out int value);
        public bool TryGetInt64(string name, out long value);
        public bool TryGetDouble(string name, out double value);
        public bool TryGetBoolean(string name, out bool value);
        public bool TryGetGuid(string name, out Guid value);
        public bool TryGetEnum<T>(string name, out T value) where T : struct, Enum;
    }

    public class ElementRule
    {
        public static readonly ElementRule Default;

        public ElementRule WithProperty(string name);
        public ElementRule WithProperties(params string[] names);
        public ElementRule WithRequired(string name);
        public ElementRule WithRequired(params string[] names);
        public ElementRule WithGroupElement(string name);
        public ElementRule WithGroupElements(params string[] names);
        public bool IsProperty(string name);
        public bool IsRequired(string name);
        public bool IsGroupElement(string name);
        public IReadOnlyCollection<string> GetRequired();
    }

    public class XmlModelCompiler
    {
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<string> Errors { get; }

        public XmlModelCompiler(string uidAttributeName = "Uid");
        public XmlModelCompiler AddRule(string elementName, ElementRule? rule = null);
        public CompiledModel Compile(string xml);
    }

Комментарий: CompiledModel — плоский набор ElementConfig, результат компиляции XML. ElementConfig — описание одного элемента (актора или вспомогательного объекта). ElementRule — правило разбора XML-элемента: какие атрибуты считать свойствами, какие обязательны, какие вложенные элементы являются группирующими. XmlModelCompiler — конфигурируемый компилятор XML в CompiledModel. Вложенные элементы без атрибутов, содержащие только текст, сохраняются как свойства родителя. Элементы без Uid пропускаются с предупреждением.

---

## 9. Реестр акторов

    internal sealed class ActorRegistry
    {
        public void Add(Actor actor);
        public void Remove(Guid uid);
        public bool TryGet(Guid uid, out Actor actor);
        public string GetName(Guid uid);
        public int Count { get; }
        public List<Actor> GetAll();
        public Task WaitForEmptyAsync();
    }

Комментарий: внутренний класс, недоступный внешнему коду (п. 10 Манифеста). Единственное место в системе с прямыми ссылками на экземпляры Actor. Потокобезопасен (ConcurrentDictionary). GetAll() возвращает снапшот. WaitForEmptyAsync завершается при опустошении реестра — используется в WaitForShutdownAsync.

---

## 10. Акторная система (интерфейс и реализация)

    public interface IActorSystem
    {
        void RegisterActor(Actor actor);
        void UnregisterActor(Guid uid);
        bool Send(Letter letter);
        void Shutdown();
        void Panic();
        Task WaitForShutdownAsync();
        CancellationToken CancellationToken { get; }
        bool IsPanic { get; }
        int ActorCount { get; }
        ILogger Logger { get; set; }
        ITimeService TimeService { get; set; }
        IActorSystemMetrics Metrics { get; set; }
        Settings Settings { get; }
        string GetActorName(Guid uid);
        List<Actor> GetAllActors();
        Actor? FindActor(Guid uid);

        // Внутренние методы для отслеживания активности (используются в TimeServiceModes.Async)
        void IncrementActivity();
        void DecrementActivity();

        // Ожидает, пока все акторы завершат обработку текущих сообщений.
        // Используется VirtualTimeService для синхронизации шагов.
        Task WaitAllIdleAsync();
    }

    public sealed class ActorSystem : IActorSystem
    {
        public ActorSystem(Settings settings);

        public ILogger Logger { get; set; }
        public ITimeService TimeService { get; set; }
        public IActorSystemMetrics Metrics { get; set; }
        public Settings Settings { get; }
        public CancellationToken CancellationToken { get; }
        public bool IsPanic { get; }
        public int ActorCount { get; }

        public void RegisterActor(Actor actor);
        public void UnregisterActor(Guid uid);
        public bool Send(Letter letter);
        public void Shutdown();
        public void Panic();
        public Task WaitForShutdownAsync();
        public string GetActorName(Guid uid);
        public List<Actor> GetAllActors();
        public Actor? FindActor(Guid uid);
    }

Комментарий: создаётся с Settings. Logger и TimeService по умолчанию — Null-реализации, заменяются через публичные сеттеры на этапе сборки. Send() при `TimeServiceModes.Sync` обрабатывает письмо синхронно через `HandleSynchronously`, при остальных режимах — через `TryEnqueue`.

RegisterActor добавляет актор в реестр и немедленно запускает его цикл RunAsync. Отдельный метод Start() отсутствует — акторы начинают работу сразу после регистрации.

Shutdown() отменяет CancellationToken. Panic() устанавливает IsPanic = true и отменяет CancellationToken. При `TimeServiceModes.System` акторы используют CancellationToken для кооперативного прерывания асинхронных операций (например, в `SystemTimeService`).

Методы IncrementActivity/DecrementActivity используются при `TimeServiceModes.Async` для отслеживания количества активных операций. WaitAllIdleAsync ожидает, пока счётчик активных операций станет нулевым — используется в `VirtualTimeService.StartVirtualClockAsync` для синхронизации шагов виртуального времени.

---

## 11. Настройки

    public enum TimeServiceModes
    {
        System,  // Продакшен, реальное время, асинхронная отправка
        Async,   // Тесты с VirtualTimeService, асинхронная отправка
        Sync     // Тесты с VirtualTimeService, синхронная обработка
    }

    public sealed class Settings
    {
        public TimeServiceModes TimeServiceModes { get; init; } = TimeServiceModes.System;
    }

Комментарий: иммутабелен после создания. `TimeServiceModes` определяет, как система взаимодействует с сервисом времени и как доставляются письма. В режиме `Sync` письма обрабатываются непосредственно в потоке отправителя (через `HandleSynchronously`), минуя очередь. В режимах `System` и `Async` письма ставятся в очередь и обрабатываются асинхронно.

---

## 12. Логгер

Используется стандартный Microsoft.Extensions.Logging.ILogger. Акторы получают его через System.Logger.

Дополнительные реализации:
- `FileLogger` — пишет логи в файл с буферизацией (сброс пачками до 100 мс);
- `CallbackLogger` — тестовая реализация, передаёт сообщения в делегат (удобно для проверки логов в тестах).

---

## 13. Метрики

    public interface IActorSystemMetrics
    {
        void MessageSent();
        void MessageDropped();
        void ActorCreated();
        void ActorDestroyed();
        void ActorCountChanged(int count);
        Meter? Meter { get; }
    }

Комментарий: метрики — заменяемый объект, аналогично логгеру и сервису времени (п. 24 Манифеста). По умолчанию используется NullMetrics — все вызовы игнорируются, Meter возвращает null.

Методы вызываются акторной системой:

- `MessageSent()` — при успешной постановке письма в очередь получателя;
- `MessageDropped()` — при отбрасывании письма из-за переполнения очереди;
- `ActorCreated()` — при регистрации нового актора в реестре;
- `ActorDestroyed()` — при удалении актора из реестра;
- `ActorCountChanged(int count)` — при изменении количества акторов в реестре.

Свойство `Meter` предоставляет стандартный Meter из System.Diagnostics.Metrics. Акторы используют его для создания собственных инструментов (счётчиков, гистограмм). Если метрики не настроены — Meter возвращает null, прикладные метрики не создаются.

Реализации:

- `NullMetrics` — null-реализация по умолчанию. Все вызовы игнорируются, Meter = null. Потокобезопасна, singleton (NullMetrics.Instance);
- `SystemMetrics` — продакшен-реализация. Создаёт Meter с именем "MinimalActorSystem" (или принимает существующий), регистрирует счётчики `messages.sent`, `messages.dropped`, `actors.created`, `actors.destroyed` и наблюдаемый датчик `actors.active`;
- `LoggerMetrics` (в MinimalActorSystem.Testing) — тестовая реализация, выводит метрики в ILogger.

## 14. Подписчик виртуального времени

    public interface IVirtualTimeSubscriber
    {
        string Name { get; }
        ValueTask OnTimeStep(DateTime currentTime);
    }

Комментарий: используется только с `VirtualTimeService`. Подписчики вызываются на каждом шаге виртуального времени для имитации внешней среды (источники телеметрии, сценарии отказов и т.п.). Подписка через `VirtualTimeService.Subscribe()`, отписка через `Unsubscribe()`.

---

## 15. Жизненный цикл

Сборка (синхронная фаза):

    ActorSystem(settings)
    system.Logger = logger
    system.TimeService = timeService
    system.Metrics = new SystemMetrics()
    new ModelActor(system)              // или CompiledModelActor
    system.RegisterActor(modelActor)    // регистрирует и запускает RunAsync
    system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model))

Запуск:

    Акторы запускаются немедленно при регистрации через RegisterActor.
    Модельный актор получает InitializeLetter:
      ModelActor:
        -> OnBuildModel()
          -> Create(актор1), Create(актор2), ...
      CompiledModelActor:
        -> CompileModel()
        -> CreateObject(элемент1), CreateObject(элемент2), ...
        -> OnAfterCreate(model)
        -> RegisterActor для всех созданных акторов
      -> система готова к работе

Штатная работа (асинхронная):

    Актор ожидает письма из очереди (ReadAsync, без занятия потока)
    При получении — OnLetter(letter)
    Отправка — System.Send(letter)

    В тестовом режиме (TimeServiceModes.Sync):
      Письма обрабатываются синхронно в потоке отправителя
      Актор не читает очередь — HandleSynchronously вызывает OnLetter напрямую

Завершение актора:

    Отмена CancellationToken
    -> выход из цикла RunAsync
    -> OnShutdown()
    -> System.UnregisterActor(Uid)

Завершение системы:

    ActorSystem.Shutdown() или Panic()
      -> _cts.Cancel()
      -> все акторы завершаются
      -> реестр пустеет
      -> WaitForShutdownAsync() завершается
      -> внешний код проверяет system.IsPanic

Аварийное завершение (Panic):

    Актор логирует ошибку, вызывает system.Panic()
    -> IsPanic = true
    -> _cts.Cancel()
    -> дальнейшее как при Shutdown
