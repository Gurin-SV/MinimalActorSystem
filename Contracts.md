# Контракты минималистичной акторной системы

Документ дополняет Манифест. Содержит сигнатуры всех сущностей, их отношения и уточняющие комментарии. Детали реализации опущены.

---

## 1. Письмо (Letter)

```csharp
public abstract class Letter
{
    public Guid Sender { get; set; }
    public Guid Receiver { get; set; }

    protected Letter(Guid sender, Guid receiver);
}
```

Комментарий: поля мутабельны для случая, когда то же письмо служит ответом —
отправитель меняет Sender/Receiver местами и отправляет обратно (п. 6 Манифеста: аккумулятор).
Все наследники — sealed (п. 12).

---

## 2. Системные письма

```csharp
public sealed class TimeServiceLetter : Letter
{
    public TimeoutCallback Callback { get; }

    public TimeServiceLetter(Guid sender, Guid receiver, TimeoutCallback callback);
}
```

Комментарий: TimeServiceLetter — уведомление о сработавшем таймауте (п. 23).
Отправитель — SystemUids.TimeService.

---

## 3. Идентификаторы системных акторов

```csharp
public sealed class SystemUids
{
    public Guid System { get; }
    public Guid TimeService { get; }
    public Guid Model { get; }
}
```

Комментарий: константные идентификаторы, доступные через экземпляр ActorSystem (п. 15).
System — системный идентификатор.
TimeService — отправитель TimeServiceLetter.
Model — идентификатор ModelActor.

---

## 4. Таймаут и сервис времени

```csharp
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
```

Комментарий: TimeoutCallback создаётся актором один раз в конструкторе.
Один и тот же экземпляр может регистрироваться многократно с разными дедлайнами.
CallbackId уникален в рамках одного актора, назначается актором.
Ключ в таблице таймаутов — (ActorUid, CallbackId).
Unregister удаляет запись полностью — и из таблицы, и из очереди ожидания.
TimeServiceLetter доставляет коллбек актору-получателю; обработка состоит в вызове
callback.Action().

---

## 5. Актор

```csharp
public abstract class Actor
{
    public Guid Uid { get; }
    public string Name { get; }
    public IActorSystem System { get; }

    protected Actor(Guid uid, string name, IActorSystem system, int queueCapacity);
    protected Actor(Guid uid, string name, IActorSystem system);
}

// Внутренние члены (доступны только ActorSystem):
internal bool TryEnqueue(Letter letter);
internal async Task RunAsync(CancellationToken ct);
internal void HandleSynchronously(Letter letter);

// Защищённые члены (переопределяются наследниками):
protected abstract Task OnLetter(Letter letter);
protected virtual Task OnShutdown() => Task.CompletedTask;
```

Комментарий: Uid — универсально уникальный идентификатор. Name — имя актора для
диагностики. System — единственная ссылка на внешний мир, через неё доступны Logger,
TimeService, Settings. Очередь писем встроена в актор (bounded, размер задаётся
в конструкторе). При переполнении очереди письма молча отбрасываются.

Цикл обработки (RunAsync) последовательно читает письма из очереди и вызывает OnLetter.
При отмене CancellationToken вызывается OnShutdown, затем актор удаляет себя из реестра.

HandleSynchronously используется в отладочном режиме (Settings.SynchronousProcessing) —
вызывает OnLetter непосредственно в потоке отправителя.

---

## 6. Системный актор

```csharp
public abstract class SystemActor : Actor
{
    protected SystemActor(Guid uid, string name, IActorSystem system);
}
```

Комментарий: системные акторы имеют константные идентификаторы из SystemUids.
Независимы от прикладной задачи. Регистрируют себя в конструкторе через
system.RegisterActor(this). Могут отправлять письма только как ответ на входящее
письмо.

---

## 7. Модельный актор (родитель прикладных акторов)

```csharp
public abstract class ModelActor : SystemActor
{
    protected ModelActor(Guid uid, string name, IActorSystem system);
    protected abstract void BuildModel();
    protected abstract Task OnModelLetter(Letter letter);
    protected Guid Create(Actor actor);
    protected void ReleaseAll();
}
```

Комментарий: ModelActor — промежуточное звено между системными и прикладными акторами
(п. 15). Имеет константный Uid. В конструкторе отправляет себе InitializeModelLetter.
При получении этого письма вызывает BuildModel, где создаёт все прикладные акторы
через Create, затем активирует их через ReleaseAll. Порядок гарантирует: на момент
отправки первого рабочего письма все акторы уже зарегистрированы в реестре.

---

## 8. Реестр акторов

```csharp
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
```

Комментарий: внутренний класс, недоступный внешнему коду (п. 10). Единственное место
в системе с прямыми ссылками на экземпляры Actor. Потокобезопасен.
GetAll() возвращает снапшот. WaitForEmptyAsync завершается при опустошении реестра.

---

## 9. Акторная система (интерфейс и реализация)

```csharp
public interface IActorSystem
{
    void Send(Letter letter);
    void RegisterActor(Actor actor);
    void UnregisterActor(Guid uid);
    Task WaitForShutdownAsync();
    CancellationToken CancellationToken { get; }
    bool IsPanic { get; }
    SystemUids Uids { get; }
    ILogger Logger { get; }
    ITimeService TimeService { get; }
    Settings Settings { get; }
}

public sealed class ActorSystem : IActorSystem
{
    public ActorSystem(Settings settings);

    public SystemUids Uids { get; }
    public ILogger Logger { get; private set; }
    public ITimeService TimeService { get; private set; }
    public Settings Settings { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsPanic { get; }

    public void SetLogger(ILogger logger);
    public void SetTimeService(ITimeService timeService);

    public void RegisterActor(Actor actor);
    public void UnregisterActor(Guid uid);
    public void Send(Letter letter);
    public void Start();
    public void Shutdown();
    public void Panic();
    public Task WaitForShutdownAsync();
}
```

Комментарий: создаётся с Settings. Logger и TimeService по умолчанию — Null-реализации,
заменяются через SetLogger/SetTimeService на этапе сборки. Send() при
Settings.SynchronousProcessing == true обрабатывает письмо синхронно в потоке отправителя.

Start() запускает циклы RunAsync для всех зарегистрированных акторов.

Shutdown() отменяет CancellationToken. Panic() устанавливает IsPanic = true и отменяет
CancellationToken. Акторы обнаруживают отмену токена, вызывают OnShutdown и удаляются
из реестра.

WaitForShutdownAsync() ожидает опустошения реестра. Должен вызываться после Start().

IsPanic позволяет внешнему коду определить причину завершения: false — Shutdown,
true — Panic.

---

## 10. Настройки

```csharp
public sealed class Settings
{
    public int DefaultQueueCapacity { get; init; } = 200;
    public bool IsProduction { get; init; } = true;
    public bool SynchronousProcessing { get; init; } = false;
}
```

Комментарий: иммутабелен после создания. DefaultQueueCapacity — размер очереди
по умолчанию. SynchronousProcessing — режим синхронной обработки писем для отладки.
В продакшене должен быть false.

---

## 11. Логгер

Используется стандартный Microsoft.Extensions.Logging.ILogger.
Акторы получают его через System.Logger.

---

## 12. Жизненный цикл

Сборка (синхронная фаза):
  ActorSystem(settings)
  system.SetLogger(logger)
  system.SetTimeService(timeService)
  new SystemActor(Uid, system)     // регистрируется в конструкторе
  new ModelActor(Uid, system)      // регистрируется, отправляет себе InitializeModelLetter

Запуск (синхронный):
  ActorSystem.Start()
    -> запуск циклов (RunAsync) всем зарегистрированным акторам
    -> ModelActor читает InitializeModelLetter
      -> BuildModel()
        -> Create(актор1), Create(актор2), ...
        -> ReleaseAll()
    -> система готова к работе

Штатная работа (асинхронная):
  Актор ожидает письма из очереди (ReadAsync, без занятия потока)
  При получении — OnLetter(letter)
  Отправка — System.Send(letter)

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
  Актор логирует CriticalError, вызывает system.Panic()
  -> IsPanic = true
  -> _cts.Cancel()
  -> дальнейшее как при Shutdown
