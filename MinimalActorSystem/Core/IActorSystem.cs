namespace MinimalActorSystem;

/// <summary>
/// Интерфейс акторной системы. Предоставляет акторам доступ к инфраструктуре:
/// отправка писем, регистрация, настройки, логгер, сервис времени и управление жизненным циклом.
/// </summary>
public interface IActorSystem
{
    /// <summary>
    /// Настройки системы. Иммутабельны после создания <see cref="ActorSystem"/>.
    /// </summary>
    Settings Settings { get; }

    /// <summary>
    /// Логгер для диагностики. Может быть заменён на этапе сборки системы.
    /// По умолчанию — пустая реализация, игнорирующая все вызовы.
    /// </summary>
    ILogger Logger { get; set; }

    /// <summary>
    /// Сервис времени. Может быть заменён на этапе сборки системы.
    /// По умолчанию — пустая реализация без обработки таймаутов.
    /// </summary>
    ITimeService TimeService { get; set; }

    /// <summary>
    /// Метрики акторной системы. Может быть заменён на этапе сборки системы.
    /// По умолчанию — пустая реализация, игнорирующая все вызовы.
    /// </summary>
    IActorSystemMetrics Metrics { get; set; }

    /// <summary>
    /// Токен отмены. Переходит в состояние <see cref="CancellationToken.IsCancellationRequested"/>
    /// при вызове <see cref="Shutdown"/> (штатное завершение) или <see cref="Panic"/> (аварийное завершение).
    /// Акторы используют его для кооперативного прерывания асинхронных операций.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Текущее количество зарегистрированных акторов.
    /// </summary>
    int ActorCount { get; }

    /// <summary>
    /// Возвращает <c>true</c>, если система была остановлена аварийно через <see cref="Panic"/>.
    /// </summary>
    bool IsPanic { get; }

    /// <summary>
    /// Регистрирует актор в системе и запускает его цикл обработки сообщений.
    /// Актор начинает принимать сообщения немедленно после регистрации.
    /// </summary>
    /// <param name="actor">Актор для регистрации.</param>
    void RegisterActor(Actor actor);

    /// <summary>
    /// Удаляет актор из реестра. Вызывается автоматически при завершении цикла обработки актора.
    /// </summary>
    /// <param name="uid">Идентификатор удаляемого актора.</param>
    void UnregisterActor(Guid uid);

    /// <summary>
    /// Отправляет письмо актору-получателю. Если получатель не найден или его очередь заполнена,
    /// письмо отбрасывается, а факт потери логируется с уровнем <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <param name="letter">Письмо для отправки. Должно содержать корректные <see cref="Letter.Sender"/> и <see cref="Letter.Receiver"/>.</param>
    /// <returns><c>true</c>, если письмо доставлено в очередь получателя; <c>false</c> в противном случае.</returns>
    bool Send(Letter letter);

    /// <summary>
    /// Запускает штатное завершение системы. Отменяет <see cref="CancellationToken"/>,
    /// что приводит к последовательному завершению всех акторов.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Запускает аварийное завершение системы. Устанавливает <see cref="IsPanic"/> в <c>true</c>
    /// и отменяет <see cref="CancellationToken"/>. Акторы завершаются так же, как при <see cref="Shutdown"/>,
    /// но внешний код может определить причину по флагу <see cref="IsPanic"/>.
    /// </summary>
    void Panic();

    /// <summary>
    /// Асинхронно ожидает, пока все акторы завершатся и реестр станет пустым.
    /// Завершается после того, как последний актор вызовет <see cref="UnregisterActor"/>.
    /// </summary>
    /// <returns>Задача, представляющая ожидание завершения системы.</returns>
    Task WaitForShutdownAsync();

    /// <summary>
    /// Возвращает имя актора по его идентификатору. Для незарегистрированных идентификаторов
    /// возвращает строковое представление <see cref="Guid"/>.
    /// </summary>
    /// <param name="uid">Идентификатор актора.</param>
    /// <returns>Имя актора.</returns>
    string GetActorName(Guid uid);

    /// <summary>
    /// Возвращает снапшот всех зарегистрированных акторов на момент вызова.
    /// </summary>
    /// <returns>Список акторов.</returns>
    List<Actor> GetAllActors();

    /// <summary>
    /// Ищет актор по идентификатору.
    /// </summary>
    /// <param name="uid">Идентификатор актора.</param>
    /// <returns>Найденный актор или <c>null</c>, если актор не зарегистрирован.</returns>
    Actor? FindActor(Guid uid);
}
