# Контракты минималистичной акторной системы

Документ дополняет Манифест. Содержит сигнатуры всех сущностей, их отношения и уточняющие комментарии. Детали реализации опущены.

---

## 1. Письмо (Letter)

```cs
public abstract class Letter
{
    public Guid Sender { get; set; }
    public Guid Receiver { get; set; }

    protected Letter(Guid sender, Guid receiver);
}
```

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

Комментарий: TimeServiceLetter — уведомление о сработавшем таймауте (п. 23). Отправитель — SystemUids.TimeService. ShutdownLetter — запрос на завершение конкретного актора. InitializeLetter — сигнал ModelActor на начало построения модели.

---

## 3. Идентификаторы системных акторов

public static class SystemUids
{
    public static readonly Guid System;
    public static readonly Guid TimeService;
    public static readonly Guid Model;
}

Комментарий: статические константные идентификаторы с фиксированными значениями. Доступны напрямую через SystemUids.System и т.д. System — системный идентификатор. TimeService — отправитель TimeServiceLetter. Model — идентификатор ModelActor.

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

Цикл обработки (RunAsync) последовательно читает письма из очереди и вызывает OnLetter. При отмене CancellationToken вызывается OnShutdown, затем актор удаляет себя из реестра.

HandleSynchronously используется в отладочном режиме (Settings.SynchronousProcessing) — вызывает OnLetter непосредственно в потоке отправителя.

---

## 6. Модельный актор (родитель прикладных акторов)

public abstract class ModelActor : Actor
{
    protected ModelActor(IActorSystem system, int queueCapacity = 256);

    // Запечатанный OnLetter — обрабатывает InitializeLetter, остальное делегирует
    protected sealed override ValueTask OnLetter(Letter letter);
    protected abstract void OnBuildModel();
    protected virtual ValueTask OnModelLetter(Letter letter);
    protected Guid Create(Actor actor);
}

Комментарий: ModelActor — промежуточное звено между системными и прикладными акторами (п. 15). Имеет константный Uid (SystemUids.Model). OnLetter запечатан (sealed override): при получении InitializeLetter вызывает OnBuildModel, при ошибке логирует и вызывает System.Panic(). Все остальные письма делегируются в OnModelLetter.

Create регистрирует дочернего актора в системе и немедленно запускает его RunAsync. Актор начинает обрабатывать сообщения сразу после регистрации.

OnModelLetter по умолчанию игнорирует письмо (возвращает default). Наследник может переопределить для обработки пользовательских сообщений.

---

## 7. Реестр акторов

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

Комментарий: внутренний класс, недоступный внешнему коду (п. 10). Единственное место в системе с прямыми ссылками на экземпляры Actor. Потокобезопасен. GetAll() возвращает снапшот. WaitForEmptyAsync завершается при опустошении реестра.

---

## 8. Акторная система (интерфейс и реализация)

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
    Settings Settings { get; }
    string GetActorName(Guid uid);
    List<Actor> GetAllActors();
    Actor? FindActor(Guid uid);
    void Trace(string message);
}

public sealed class ActorSystem : IActorSystem
{
    public ActorSystem(Settings settings);

    public ILogger Logger { get; set; }
    public ITimeService TimeService { get; set; }
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

Комментарий: создаётся с Settings. Logger и TimeService по умолчанию — Null-реализации, заменяются через публичные сеттеры на этапе сборки. Send() при Settings.SynchronousProcessing == true обрабатывает письмо синхронно в потоке отправителя.

RegisterActor добавляет актор в реестр и немедленно запускает его цикл RunAsync. Отдельный метод Start() отсутствует — акторы начинают работу сразу после регистрации.

Shutdown() отменяет CancellationToken. Panic() устанавливает IsPanic = true и отменяет CancellationToken. Акторы обнаруживают отмену токена, вызывают OnShutdown и удаляются из реестра.

WaitForShutdownAsync() ожидает опустошения реестра.

IsPanic позволяет внешнему коду определить причину завершения: false — Shutdown, true — Panic.

Trace — внутренний метод для диагностического логирования (активируется при определении символа TRACE_ACTORS).

---

## 9. Настройки

public sealed class Settings
{
    public bool IsProduction { get; init; } = true;
    public bool SynchronousProcessing { get; init; } = false;
}

Комментарий: иммутабелен после создания. SynchronousProcessing — режим синхронной обработки писем для отладки. В продакшене должен быть false.

---

## 10. Логгер

Используется стандартный Microsoft.Extensions.Logging.ILogger. Акторы получают его через System.Logger.

---

## 11. Жизненный цикл

Сборка (синхронная фаза):
  ActorSystem(settings)
  system.Logger = logger
  system.TimeService = timeService
  new ModelActor(system)              // регистрируется в конструкторе
  model.Initialize()                  // отправляет InitializeLetter самому себе

Запуск:
  Акторы запускаются немедленно при регистрации через RegisterActor.
  ModelActor получает InitializeLetter:
    -> OnBuildModel()
      -> Create(актор1), Create(актор2), ...
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
  Актор логирует ошибку, вызывает system.Panic()
  -> IsPanic = true
  -> _cts.Cancel()
  -> дальнейшее как при Shutdown
