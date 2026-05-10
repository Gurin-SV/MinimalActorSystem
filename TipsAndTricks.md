# Советы по использованию MinimalActorSystem

Этот документ собирает практические приёмы и паттерны, которые упрощают разработку прикладных акторов. Каждый совет независим — можно читать выборочно, под задачу.

## 1. Circuit Breaker для внешних вызовов

Если ваш актор обращается к внешнему HTTP-сервису, базе данных, очереди сообщений или файловой системе — рано или поздно этот ресурс станет недоступен. Без защиты актор будет безуспешно пытаться снова и снова: тратить ресурсы, забивать очередь, плодить ошибки в логах. Circuit Breaker решает это: после N последовательных ошибок он отключает попытки на заданную паузу. Вызова нет — ошибок нет. Через паузу он пропускает один пробный запрос — и либо восстанавливает работу, либо снова уходит в паузу.

### Три состояния

- **Closed (Замкнут)** — нормальная работа. Запросы проходят к ресурсу. Считаются последовательные ошибки. Когда счётчик достигает порога — переход в Open.
- **Open (Разомкнут)** — запросы не проходят. Вместо реального вызова сразу возвращается отказ. Через заданный таймаут — переход в HalfOpen.
- **HalfOpen (Полузамкнут)** — пропускается один пробный запрос. Успех — переход в Closed, счётчик ошибок сбрасывается. Ошибка — обратно в Open.

### Реализация

Breaker — это не отдельный актор, а поведение внутри вашего актора. Вам нужны: поле состояния, счётчик ошибок, таймаут паузы и коллбек для него.

Создайте коллбек таймаута в конструкторе:

    private readonly TimeoutCallback _openTimeoutCallback;
    private State _state = State.Closed;
    private int _failureCount;
    private const int Threshold = 5;
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);

    public ServiceCallerActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _openTimeoutCallback = new TimeoutCallback(Uid, callbackId: 1, OnOpenTimeout);
    }

    private void OnOpenTimeout()
    {
        _state = State.HalfOpen;
    }

Обрабатывайте запросы в зависимости от состояния:

    private void HandleRequest(ServiceRequest req)
    {
        switch (_state)
        {
            case State.Open:
                RespondFailure(req);
                break;

            case State.Closed:
            case State.HalfOpen:
                SendToExternalService(req);
                break;
        }
    }

При успехе — сброс:

    private void HandleSuccess()
    {
        _failureCount = 0;
        _state = State.Closed;
        System.TimeService.Unregister(_openTimeoutCallback);
    }

При ошибке — считаем и выбиваем при достижении порога:

    private void HandleFailure()
    {
        _failureCount++;

        if (_state == State.HalfOpen)
        {
            _state = State.Open;
            System.TimeService.Register(OpenTimeout, _openTimeoutCallback);
            return;
        }

        if (_state == State.Closed && _failureCount >= Threshold)
        {
            _state = State.Open;
            System.TimeService.Register(OpenTimeout, _openTimeoutCallback);
        }
    }

### Почему это работает без блокировок

Состояние, счётчик и флаги — обычные поля экземпляра актора. Актор обрабатывает сообщения строго по одному, поэтому два потока никогда не заходят в `HandleRequest` одновременно. Никаких `lock`, `Interlocked` или `ConcurrentDictionary` не нужно — модель актора сама гарантирует последовательный доступ.

### Тестирование

С виртуальным временем все переходы проверяются мгновенно. Установите `Threshold` в 2, `OpenTimeout` в 10 секунд, отправьте три запроса подряд (два фейла — цепь рвётся), продвиньте время на 10 секунд (переход в HalfOpen), отправьте ещё один — и проверьте, пошёл ли он к сервису или сразу вернул отказ.

---

## 2. Рандеву с таймаутом

Часто актору нужно не просто послать письмо и забыть, а дождаться ответа от другого актора — и продолжить алгоритм только после получения ответа или истечения времени ожидания. Например: запросили конфигурацию у ConfigActor — ждём ответ, чтобы начать работу; запросили разрешение у CoordinatorActor — ждём, чтобы выполнить операцию; отправили запрос к внешнему шлюзу — ждём результат.

Рандеву (Rendez-vous) — это паттерн временного ожидания ответа с таймаутом. Актор-инициатор отправляет письмо, регистрирует таймаут и переходит в режим ожидания. Когда приходит ответ — таймаут отменяется, алгоритм продолжается. Когда таймаут срабатывает — актор реагирует на отсутствие ответа.

### Состояние ожидания

Для рандеву актору нужно состояние — кого и чего он ждёт. Самое простое решение: хранить ожидаемый тип ответа и контекст запроса.

    private Type? _expectedResponseType;
    private object? _pendingContext;

Актор находится в ожидании, пока `_expectedResponseType` не null. Все остальные письма в этот момент либо игнорируются, либо ставятся в отложенную очередь (если логика требует строгой очерёдности). Для начала рассмотрим вариант с игнорированием — он проще.

### Коллбек таймаута

Создаётся в конструкторе. При срабатывании вызывает метод `OnRendezvousTimeout`:

    private readonly TimeoutCallback _rendezvousTimeout;

    public WorkerActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _rendezvousTimeout = new TimeoutCallback(Uid, callbackId: 1, OnRendezvousTimeout);
    }

    private void OnRendezvousTimeout()
    {
        var context = _pendingContext;
        _expectedResponseType = null;
        _pendingContext = null;

        System.Logger.LogWarning("{Name}: рандеву с {Type} истекло по таймауту", Name, _expectedResponseType?.Name);
        HandleTimeout(context);
    }

Метод `HandleTimeout` — реакция на отсутствие ответа. Это может быть повтор запроса, возврат ошибки отправителю, переход в аварийный режим или просто логирование.

### Отправка запроса и вход в рандеву

    private static readonly TimeSpan DefaultRendezvousTimeout = TimeSpan.FromSeconds(30);

    public void RequestConfig(Guid configActorUid)
    {
        if (_expectedResponseType != null)
        {
            System.Logger.LogWarning("{Name}: уже в рандеву, запрос отклонён", Name);
            return;
        }

        _expectedResponseType = typeof(ConfigResponse);
        _pendingContext = null;  // или какой-то объект контекста

        System.TimeService.Register(DefaultRendezvousTimeout, _rendezvousTimeout);
        System.Send(new ConfigRequest(Uid, configActorUid));
    }

### Обработка ответа

В `OnLetter` добавляем проверку — если актор в рандеву, он обрабатывает только ожидаемый тип:

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case ConfigResponse resp when _expectedResponseType == typeof(ConfigResponse):
                // Ответ получен — отменяем таймаут, выходим из рандеву
                System.TimeService.Unregister(_rendezvousTimeout);
                _expectedResponseType = null;
                _pendingContext = null;

                // Продолжаем алгоритм
                OnConfigReceived(resp);
                break;

            case TimeServiceLetter timeout:
                timeout.Callback.Action();
                break;

            default:
                // В режиме ожидания остальные письма игнорируем
                if (_expectedResponseType != null)
                {
                    System.Logger.LogDebug("{Name}: в рандеву, игнорирую {Type}", Name, letter.GetType().Name);
                    break;
                }
                HandleOtherLetter(letter);
                break;
        }
        return default;
    }

### Повтор с нарастающей паузой

Таймаут рандеву можно совместить с повтором запроса. После срабатывания таймаута актор увеличивает счётчик попыток и отправляет запрос снова — с увеличенной паузой.

    private int _retryCount;
    private static readonly TimeSpan[] RetryBackoff = 
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    };

    private void OnRendezvousTimeout()
    {
        if (_retryCount < RetryBackoff.Length)
        {
            var target = _pendingTarget;  // запомнили кому слать
            System.TimeService.Register(RetryBackoff[_retryCount], _rendezvousTimeout);
            _retryCount++;
            System.Send(new ConfigRequest(Uid, target));
        }
        else
        {
            _expectedResponseType = null;
            _pendingContext = null;
            _retryCount = 0;
            HandleDefinitiveTimeout();
        }
    }

При получении ответа счётчик попыток сбрасывается.

### Тестирование

С виртуальным временем рандеву проверяется легко. Сценарий «ответ пришёл вовремя»: отправляем запрос, продвигаем время на 5 секунд (меньше таймаута), отправляем ответ от имени второго актора через `System.Send` — проверяем, что таймаут отменён и алгоритм продолжен. Сценарий «таймаут»: отправляем запрос, продвигаем время за таймаут, проверяем что вызван `HandleTimeout`. Сценарий «повтор»: продвигаем время до срабатывания таймаута, проверяем что запрос отправлен снова, затем доставляем ответ — проверяем что счётчик сброшен.

### Когда использовать

- Запрос конфигурации, разрешения, квоты у другого актора
- Ожидание результата от актора-шлюза к внешнему сервису
- Координация двух акторов, где один делает запрос и ждёт подтверждения
- Любое взаимодействие типа «запрос-ответ» внутри системы

Рандеву — это локальное ожидание внутри одного актора. Оно не блокирует поток, не ставит на паузу других акторов и не требует системных примитивов вроде `TaskCompletionSource`. Актор просто ждёт либо нужное письмо, либо таймаут — что наступит раньше.

---

## 3. Аккумулятор — перемещаемая изменяемая посылка

Иногда сообщение должно пройти по цепочке акторов, и каждый добавляет к нему свою часть данных. Вместо того чтобы каждый актор создавал новое письмо и копировал данные, можно использовать один экземпляр письма, который передаётся от актора к актору, мутируя по пути.

Контракты системы явно разрешают это: поля `Sender` и `Receiver` мутабельны, а письмо можно переиспользовать. Каждый актор в цепочке меняет `Sender`/`Receiver` местами или подставляет новые идентификаторы, добавляет свои данные в поля письма и отправляет дальше.

### Пример: сбор подписей под документом

Письмо-аккумулятор:

    public sealed class ApprovalChain : Letter
    {
        public List<ApprovalStep> Steps { get; } = new();
        public bool IsApproved => Steps.All(s => s.Approved);

        public ApprovalChain(Guid sender, Guid receiver) : base(sender, receiver) { }
    }

    public sealed class ApprovalStep
    {
        public Guid ApproverUid { get; init; }
        public string ApproverName { get; init; }
        public bool Approved { get; set; }
        public string? Comment { get; set; }
    }

Первый актор создаёт письмо и запускает цепочку:

    var chain = new ApprovalChain(Uid, firstApproverUid);
    chain.Steps.Add(new ApprovalStep { ApproverUid = firstApproverUid, ApproverName = "Manager" });
    chain.Steps.Add(new ApprovalStep { ApproverUid = secondApproverUid, ApproverName = "Director" });
    System.Send(chain);

Актор-согласователь получает письмо, находит свой шаг, ставит резолюцию и отправляет дальше:

    protected override ValueTask OnLetter(Letter letter)
    {
        if (letter is ApprovalChain chain)
        {
            var myStep = chain.Steps.First(s => s.ApproverUid == Uid);
            myStep.Approved = true;
            myStep.Comment = "Одобряю";

            // Отправляем следующему или возвращаем инициатору
            var nextStep = chain.Steps.FirstOrDefault(s => !s.Approved);
            if (nextStep != null)
            {
                chain.Sender = Uid;
                chain.Receiver = nextStep.ApproverUid;
            }
            else
            {
                chain.Sender = Uid;
                chain.Receiver = chain.Steps[0].ApproverUid;  // обратно инициатору
            }
            System.Send(chain);
        }
        return default;
    }

Никакого копирования, никаких аллокаций на каждом шаге. Письмо накапливает состояние и движется по цепочке.

### Когда полезно

- Цепочки обработки (каждый добавляет свой этап)
- Сбор результатов параллельных запросов (один актор-координатор)
- Формирование составного ответа из нескольких источников

### Осторожность

Аккумулятор нарушает иммутабельность сообщений. Если письмо случайно уйдёт в два места одновременно — начнётся гонка. Используйте только для строго последовательных цепочек или когда владелец письма — единственный писатель.

---

## 4. Делегат — актор без состояния под задачу

Иногда нужно отреагировать на одно конкретное событие или выполнить одно действие — и создавать полноценного наследника `Actor` с классом кажется избыточным. Паттерн «Делегат» — это актор, которому передаётся лямбда или делегат в конструктор, и он вызывает его при получении письма.

### Простой делегат

    public sealed class DelegateActor : Actor
    {
        private readonly Func<Letter, ValueTask> _handler;

        public DelegateActor(IActorSystem system, Guid uid, string name,
            Func<Letter, ValueTask> handler)
            : base(system, uid, name)
        {
            _handler = handler;
        }

        protected override ValueTask OnLetter(Letter letter) => _handler(letter);
    }

Использование:

    var actor = new DelegateActor(system, Guid.NewGuid(), "one-shot",
        async letter =>
        {
            if (letter is SomeResponse resp)
            {
                // обработать ответ
            }
        });

### Делегат с таймером

Можно создать актор, который просто ждёт заданное время и выполняет действие:

    public sealed class DelayedActionActor : Actor
    {
        private readonly Action _action;
        private readonly TimeoutCallback _timer;

        public DelayedActionActor(IActorSystem system, Guid uid, string name,
            TimeSpan delay, Action action)
            : base(system, uid, name)
        {
            _action = action;
            _timer = new TimeoutCallback(Uid, callbackId: 1, OnTimer);
            system.TimeService.Register(delay, _timer);
        }

        private void OnTimer()
        {
            _action();
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeout)
                timeout.Callback.Action();
            return default;
        }
    }

После выполнения действия актор удаляет себя из реестра.

### Когда полезно

- Одноразовые реакции (запросили — ответили — забыли)
- Адаптеры между разными интерфейсами писем
- Тестовые заглушки, которым не нужен полноценный класс

---

## 5. Маршрутизатор — письмо по критерию

Когда в системе несколько однотипных акторов, отправитель не всегда знает, кому именно адресовать письмо. Маршрутизатор — это актор, который получает письмо, определяет по его содержимому целевого получателя и пересылает.

### Пример: Round-robin балансировщик

    public sealed class RoundRobinRouter : Actor
    {
        private readonly List<Guid> _workers;
        private int _index;

        public RoundRobinRouter(IActorSystem system, Guid uid, string name,
            IEnumerable<Guid> workers)
            : base(system, uid, name)
        {
            _workers = workers.ToList();
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (_workers.Count == 0)
                return default;

            var target = _workers[_index];
            _index = (_index + 1) % _workers.Count;

            letter.Sender = Uid;
            letter.Receiver = target;
            System.Send(letter);
            return default;
        }
    }

### Пример: Маршрутизация по ключу

    public sealed class ShardRouter : Actor
    {
        private readonly Dictionary<int, Guid> _shards;

        public ShardRouter(IActorSystem system, Guid uid, string name,
            Dictionary<int, Guid> shards)
            : base(system, uid, name)
        {
            _shards = shards;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is IShardable shardable)
            {
                var shardKey = shardable.ShardKey;
                if (_shards.TryGetValue(shardKey, out var target))
                {
                    letter.Sender = Uid;
                    letter.Receiver = target;
                    System.Send(letter);
                }
            }
            return default;
        }
    }

Интерфейс `IShardable` — это маркер на письме, который позволяет роутеру извлечь ключ шардирования без знания конкретного типа.

### Когда полезно

- Пул однотипных обработчиков (балансировка нагрузки)
- Шардирование данных по акторам-владельцам
- Условная маршрутизация: критические письма — одним акторам, обычные — другим

---

## 6. Сторож — контроль здоровья дочернего актора

Родитель создаёт дочернего актора и хочет знать, жив ли он. Сторож периодически отправляет дочернему актору `Ping` и ждёт `Pong`. Если ответ не приходит за отведённое время — дочерний считается мёртвым, и родитель принимает меры.

В отличие от системного супервизора, Сторож — это прикладной паттерн, встроенный в логику родителя. Никакого привилегированного доступа к дочернему актору не нужно.

### Реализация

    private readonly TimeoutCallback _watchdogTimer;
    private Guid _childUid;
    private bool _pongReceived;

    public ParentActor(IActorSystem system, Guid uid, string name)
        : base(system, uid, name)
    {
        _watchdogTimer = new TimeoutCallback(Uid, callbackId: 1, OnWatchdogTimeout);
    }

    private void StartWatching(Guid childUid)
    {
        _childUid = childUid;
        SendPing();
    }

    private void SendPing()
    {
        _pongReceived = false;
        System.TimeService.Register(TimeSpan.FromSeconds(5), _watchdogTimer);
        System.Send(new Ping(Uid, _childUid));
    }

    private void OnWatchdogTimeout()
    {
        if (!_pongReceived)
        {
            System.Logger.LogWarning("{Name}: дочерний актор {Child} не отвечает", Name, GetChildName());
            HandleChildLost();
        }
    }

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case Pong pong when pong.Sender == _childUid:
                _pongReceived = true;
                System.TimeService.Unregister(_watchdogTimer);
                // Через некоторое время — снова проверка
                ScheduleNextPing();
                break;

            case TimeServiceLetter timeout:
                timeout.Callback.Action();
                break;
        }
        return default;
    }

### Когда полезно

- Дочерний актор работает с ненадёжным ресурсом и может застрять
- Длительные фоновые операции, за которыми нужно следить
- Мягкая деградация: потеряли обработчик — создали новый

---

## 7. Отложенная очередь — письма на будущее

Актор может получить письмо, которое нельзя обработать прямо сейчас (не готов, нет данных, ждём другого письма). Вместо того чтобы игнорировать или ронять ошибку, актор ставит письмо в отложенную очередь и возвращается к нему позже.

В отличие от рандеву, где актор ждёт ровно один ответ, здесь актор продолжает обрабатывать другие письма, а отложенные пересматривает по событию или таймеру.

### Реализация с двойной очередью

    private readonly Queue<Letter> _deferred = new();

    protected override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case DataReady ready:
                // Данные готовы — обрабатываем отложенные
                await ProcessDeferred();
                break;

            case Request req when !_dataReady:
                // Ещё не готовы — откладываем
                _deferred.Enqueue(req);
                break;

            case Request req:
                // Готовы — обрабатываем сразу
                HandleRequest(req);
                break;
        }
    }

    private async Task ProcessDeferred()
    {
        while (_deferred.Count > 0)
        {
            var letter = _deferred.Dequeue();
            if (letter is Request req)
                HandleRequest(req);
        }
    }

### Когда полезно

- Буферизация запросов до завершения инициализации
- Накопление данных для пакетной обработки
- Переупорядочивание: пришло раньше срока — полежи до нужного времени

---

## 8. Заглушка (Stub) для тестов

При тестировании актора, который зависит от другого актора, удобно подставить заглушку — актора, который принимает письма и либо ничего не делает, либо отвечает заранее заданным образом.

### Пустая заглушка

Принимает любые письма и молчит:

    public sealed class NullActor : Actor
    {
        public NullActor(IActorSystem system, Guid uid) 
            : base(system, uid, "null") { }

        protected override ValueTask OnLetter(Letter letter) => default;
    }

### Заглушка с ответом

При получении письма определённого типа отвечает заданным письмом:

    public sealed class StubActor : Actor
    {
        private readonly Type _expectedType;
        private readonly Letter _response;

        public StubActor(IActorSystem system, Guid uid, Type expectedType, Letter response)
            : base(system, uid, "stub")
        {
            _expectedType = expectedType;
            _response = response;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter.GetType() == _expectedType)
            {
                _response.Sender = Uid;
                _response.Receiver = letter.Sender;
                System.Send(_response);
            }
            return default;
        }
    }

Использование в тесте:

    var stub = new StubActor(system, Guid.NewGuid(),
        typeof(ConfigRequest),
        new ConfigResponse(Uid: Guid.Empty, receiver: Guid.Empty, configData: "..."));
    system.RegisterActor(stub);

    // Тестируемый актор посылает ConfigRequest в stub
    // и получает ConfigResponse с заданными данными

### Когда полезно

- Изоляция тестируемого актора от реальных зависимостей
- Эмуляция ответов внешних шлюзов
- Проверка что актор отправляет письма нужного типа и с нужными данными

---

