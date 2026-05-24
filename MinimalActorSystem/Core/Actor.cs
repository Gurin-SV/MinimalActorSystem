namespace MinimalActorSystem;

/// <summary>
/// Базовый класс актора. Инкапсулирует очередь сообщений, цикл обработки и жизненный цикл.
/// Наследники переопределяют <see cref="OnLetter"/> для обработки входящих писем
/// и опционально <see cref="OnShutdown"/> для освобождения ресурсов при завершении.
/// </summary>
/// <remarks>
/// Base actor class. Encapsulates message queue, processing loop, and lifecycle.
/// </remarks>
public abstract class Actor
{
    private readonly Channel<Letter> _channel;

    /// <summary>
    /// Размер очереди сообщений по умолчанию.
    /// </summary>
    public const int DefaultQueueCapacity = 256;

    /// <summary>
    /// Ссылка на акторную систему. Единственный способ взаимодействия актора с внешним миром.
    /// </summary>
    public IActorSystem System { get; }

    /// <summary>
    /// Уникальный идентификатор актора. Назначается при создании и не изменяется.
    /// </summary>
    public Guid Uid { get; }

    /// <summary>
    /// Имя актора для диагностики и логирования.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Максимальное количество писем сообщений в очереди. При превышении новые письма отбрасываются, но при
    /// попытке записи в уже заполненную очередь Send возвращает false.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Количество писем в очереди
    /// </summary>
    public int QueueSize => _channel.Reader.Count;

    /// <summary>
    /// Создаёт актор с указанными параметрами и инициализирует очередь сообщений.
    /// </summary>
    /// <param name="system">Акторная система, которой принадлежит актор.</param>
    /// <param name="uid">Уникальный идентификатор актора.</param>
    /// <param name="name">Имя актора для диагностики.</param>
    /// <param name="queueCapacity">Максимальный размер очереди сообщений.</param>
    protected Actor(IActorSystem system, Guid uid, string name, int queueCapacity = DefaultQueueCapacity)
    {
        if (uid == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(uid), "Invalid uid");
        if (queueCapacity < 1 || queueCapacity > 100_000)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Invalid queue capacity");

        System = system;
        Uid = uid;
        Name = name;
        QueueCapacity = queueCapacity;

        var options = new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<Letter>(options);
    }

    /// <summary>
    /// Пытается поместить письмо в очередь актора. Вызывается акторной системой.
    /// </summary>
    /// <param name="letter">Письмо для доставки.</param>
    /// <returns><c>true</c>, если письмо помещено в очередь; <c>false</c>, если очередь заполнена.</returns>
    /// <remarks>
    /// Called by actor system. Returns false if queue is full.
    /// </remarks>
    internal bool TryEnqueue(Letter letter)
    {
        if (!_channel.Writer.TryWrite(letter))
            return false;
        if (System.Settings.TimeServiceModes == TimeServiceModes.Async)
        {
            ((IActorSystemInternal)System).IncrementActivity();
        }
        return true;
    }

    /// <summary>
    /// Запускает цикл обработки сообщений. Выполняется до отмены токена.
    /// При завершении вызывает <see cref="OnShutdown"/> и удаляет актор из реестра.
    /// </summary>
    /// <param name="ct">Токен отмены, связанный с жизненным циклом акторной системы.</param>
    /// <remarks>
    /// Runs main message loop until cancellation. On exit, calls OnShutdown and unregisters the actor.
    /// </remarks>
    internal async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var letter = await _channel.Reader.ReadAsync(ct);
                if (letter is ShutdownLetter)
                {
                    break;
                }

                try
                {
                    var task = OnLetter(letter);
                    if (!task.IsCompletedSuccessfully)
                        await task;
                }
                catch (Exception ex)
                {
                    System.Logger.LogError(ex, "Error in actor {Name}", Name);
                }
                finally
                {
                    if (System.Settings.TimeServiceModes == TimeServiceModes.Async)
                        ((IActorSystemInternal)System).DecrementActivity();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        try
        {
            await OnShutdown();
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Error in OnShutdown for actor {Name}", Name);
        }
        System.UnregisterActor(Uid);
    }

    /// <summary>
    /// Синхронно обрабатывает письмо в потоке отправителя.
    /// Используется только в отладочном режиме при <c>Settings.SynchronousProcessing == true</c>.
    /// </summary>
    /// <param name="letter">Письмо для обработки.</param>
    /// <remarks>
    /// Synchronously processes a message in caller's thread. Debug mode only.
    /// </remarks>
    internal void HandleSynchronously(Letter letter)
    {
        try
        {
            var task = OnLetter(letter);
            if (!task.IsCompletedSuccessfully)
                task.AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Error in actor {Name}", Name);
        }
    }

    /// <summary>
    /// Обрабатывает входящее письмо. Вызывается акторной системой для каждого письма из очереди.
    /// Наследники обязаны переопределить этот метод.
    /// Реализация должна:
    ///  - Не блокировать бесконечно
    ///  - Либо периодически проверять System.CancellationToken для кооперативной отмены
    ///  - Либо использовать перегрузки await с токеном отмены где возможно
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    /// <remarks>
    /// Must be overridden. Handles a single message. Should be non-blocking and cooperative with cancellation.
    /// </remarks>
    protected abstract ValueTask OnLetter(Letter letter);

    /// <summary>
    /// Вызывается при завершении актора перед удалением из реестра.
    /// Наследники могут переопределить для освобождения ресурсов.
    /// </summary>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию завершения.</returns>
    /// <remarks>
    /// Optional override for resource cleanup. Called during shutdown before unregistering.
    /// </remarks>
    protected virtual ValueTask OnShutdown() => default;
}
