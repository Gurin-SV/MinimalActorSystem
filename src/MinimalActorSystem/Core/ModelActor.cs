namespace MinimalActorSystem;

/// <summary>
/// Модельный актор — корневой родитель прикладных акторов. Является промежуточным звеном
/// между системными и прикладными акторами. Имеет константный идентификатор <see cref="SystemUids.Model"/>.
/// При получении <see cref="InitializeLetter"/> вызывает <see cref="OnBuildModel"/> для создания
/// прикладных акторов. При ошибке построения модели вызывает <see cref="IActorSystem.Panic"/>.
/// Все остальные письма делегируются в <see cref="OnModelLetter"/>.
/// </summary>
/// <remarks>
/// Root parent for application actors. Has constant UID <see cref="SystemUids.Model"/>.
/// On <see cref="InitializeLetter"/>, calls <see cref="OnBuildModel"/> to create child actors.
/// On build error, calls <see cref="IActorSystem.Panic"/>.
/// Other letters are delegated to <see cref="OnModelLetter"/>.
/// </remarks>
/// <param name="system">Акторная система.</param>
/// <param name="queueCapacity">Размер очереди сообщений.</param>
public abstract class ModelActor(IActorSystem system, int queueCapacity = 256)
    : Actor(system, SystemUids.Model, "model", queueCapacity)
{
    /// <summary>
    /// Запечатанный обработчик писем. Обрабатывает <see cref="InitializeLetter"/> вызовом <see cref="OnBuildModel"/>,
    /// все остальные письма делегирует в <see cref="OnModelLetter"/>.
    /// Наследники не могут переопределить этот метод — вместо этого они переопределяют
    /// <see cref="OnBuildModel"/> и <see cref="OnModelLetter"/>.
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    /// <remarks>
    /// Sealed message handler. Handles InitializeLetter via OnBuildModel,
    /// delegates other letters to OnModelLetter. Cannot be overridden by descendants.
    /// </remarks>
    protected sealed override async ValueTask OnLetter(Letter letter)
    {
        if (letter is InitializeLetter)
        {
            try
            {
                OnBuildModel();
            }
            catch (Exception ex)
            {
                System.Logger.LogError(ex, "Model build failed, triggering panic");
                System.Panic();
            }
        }
        else
        { 
            await OnModelLetter(letter);
        }
    }

    /// <summary>
    /// Вызывается при получении <see cref="InitializeLetter"/>. Наследники обязаны переопределить
    /// этот метод для создания прикладных акторов через <see cref="Create"/>.
    /// Метод синхронный: модель либо строится целиком, либо не строится вовсе.
    /// При возникновении исключения вызывается <see cref="IActorSystem.Panic"/>.
    /// </summary>
    /// <remarks>
    /// Called on InitializeLetter. Must be overridden to create application actors via <see cref="Create"/>.
    /// Synchronous: model is built entirely or not at all. Throwing an exception triggers Panic.
    /// </remarks>
    protected abstract void OnBuildModel();

    /// <summary>
    /// Обрабатывает все письма, не являющиеся <see cref="InitializeLetter"/>.
    /// По умолчанию игнорирует письмо (возвращает завершённый <see cref="ValueTask"/>).
    /// Наследники могут переопределить для обработки пользовательских сообщений.
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    /// <remarks>
    /// Handles all non-InitializeLetter messages. Default implementation ignores the message.
    /// Override to handle application-specific messages.
    /// </remarks>
    protected virtual ValueTask OnModelLetter(Letter letter) => default;

    /// <summary>
    /// Регистрирует дочернего актора в системе и немедленно запускает его цикл обработки.
    /// Должен вызываться только из <see cref="OnBuildModel"/>.
    /// </summary>
    /// <param name="actor">Дочерний актор для регистрации.</param>
    /// <returns>Идентификатор зарегистрированного актора.</returns>
    /// <remarks>
    /// Registers a child actor in the system and starts its message loop.
    /// Must only be called from <see cref="OnBuildModel"/>.
    /// </remarks>
    protected Guid Create(Actor actor)
    {
        System.RegisterActor(actor);
        return actor.Uid;
    }
}
