using System.Diagnostics;

namespace MinimalActorSystem;

/// <summary>
/// Базовый класс актора. Инкапсулирует очередь сообщений, цикл обработки и жизненный цикл.
/// Наследники переопределяют <see cref="OnLetter"/> для обработки входящих писем
/// и опционально <see cref="OnShutdown"/> для освобождения ресурсов при завершении.
/// </summary>
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
    /// Максимальное количество сообщений в очереди. При превышении новые сообщения отбрасываются.
    /// </summary>
    public int QueueCapacity { get; }

    /// <summary>
    /// Создаёт актор с указанными параметрами и инициализирует очередь сообщений.
    /// </summary>
    /// <param name="system">Акторная система, которой принадлежит актор.</param>
    /// <param name="uid">Уникальный идентификатор актора.</param>
    /// <param name="name">Имя актора для диагностики.</param>
    /// <param name="queueCapacity">Максимальный размер очереди сообщений.</param>
    protected Actor(IActorSystem system, Guid uid, string name, int queueCapacity = DefaultQueueCapacity)
    {
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
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    protected abstract ValueTask OnLetter(Letter letter);

    /// <summary>
    /// Вызывается при завершении актора перед удалением из реестра.
    /// Наследники могут переопределить для освобождения ресурсов.
    /// </summary>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию завершения.</returns>
    protected virtual ValueTask OnShutdown() => default;
}
