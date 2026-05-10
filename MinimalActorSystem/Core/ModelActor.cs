namespace MinimalActorSystem;

/// <summary>
/// Модельный актор — корневой родитель прикладных акторов. Является промежуточным звеном
/// между системными и прикладными акторами. Имеет константный идентификатор <see cref="SystemUids.Model"/>.
/// При получении <see cref="InitializeLetter"/> вызывает <see cref="OnBuildModel"/> для создания
/// прикладных акторов. При ошибке построения модели вызывает <see cref="IActorSystem.Panic"/>.
/// Все остальные письма делегируются в <see cref="OnModelLetter"/>.
/// </summary>
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
    protected sealed override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case InitializeLetter:
                try
                {
                    OnBuildModel();
                }
                catch (Exception ex)
                {
                    System.Logger.LogError(ex, "Model build failed, triggering panic");
                    System.Panic();
                }
                break;

            default:
                await OnModelLetter(letter);
                break;
        }
    }

    /// <summary>
    /// Вызывается при получении <see cref="InitializeLetter"/>. Наследники обязаны переопределить
    /// этот метод для создания прикладных акторов через <see cref="Create"/>.
    /// Метод синхронный: модель либо строится целиком, либо не строится вовсе.
    /// При возникновении исключения вызывается <see cref="IActorSystem.Panic"/>.
    /// </summary>
    protected abstract void OnBuildModel();

    /// <summary>
    /// Обрабатывает все письма, не являющиеся <see cref="InitializeLetter"/>.
    /// По умолчанию игнорирует письмо (возвращает завершённый <see cref="ValueTask"/>).
    /// Наследники могут переопределить для обработки пользовательских сообщений.
    /// </summary>
    /// <param name="letter">Входящее письмо.</param>
    /// <returns><see cref="ValueTask"/>, представляющий асинхронную операцию обработки.</returns>
    protected virtual ValueTask OnModelLetter(Letter letter) => default;

    /// <summary>
    /// Регистрирует дочернего актора в системе и немедленно запускает его цикл обработки.
    /// Должен вызываться только из <see cref="OnBuildModel"/>.
    /// </summary>
    /// <param name="actor">Дочерний актор для регистрации.</param>
    /// <returns>Идентификатор зарегистрированного актора.</returns>
    protected Guid Create(Actor actor)
    {
        System.RegisterActor(actor);
        return actor.Uid;
    }
}
