namespace MinimalActorSystem.XmlModel;

/// <summary>
/// Модельный актор, собирающий систему из <see cref="CompiledModel"/>.
/// Альтернатива <see cref="ModelActor"/> для декларативного построения модели из XML.
/// В одной акторной системе может существовать только один модельный актор.
/// 
/// Жизненный цикл построения модели:
/// 1. Актор получает <see cref="InitializeLetter"/>.
/// 2. Вызывается <see cref="CompileModel"/> — наследник компилирует XML в <see cref="CompiledModel"/>.
/// 3. <see cref="CreateObject"/> — для каждого элемента модели создаётся объект (актор или вспомогательный).
///    Все объекты помещаются во временный словарь.
/// 4. <see cref="OnAfterCreate"/> — наследник устанавливает связи между объектами.
/// 5. Все акторы регистрируются в системе через <see cref="IActorSystem.RegisterActor"/>.
/// 6. Модель готова к работе. Остальные письма обрабатываются в <see cref="OnModelLetter"/>.
/// 7. При ошибке на любом этапе — логирование и <see cref="IActorSystem.Panic"/>.
/// </summary>
/// <remarks>
/// Model actor that builds the system from a CompiledModel.
/// Alternative to ModelActor for declarative XML-based model construction.
/// Only one model actor can exist per actor system.
/// 
/// Build lifecycle:
/// 1. Receives InitializeLetter.
/// 2. CompileModel() is called — descendant compiles XML into CompiledModel.
/// 3. CreateObject() creates an object for each model element (actor or helper).
///    All objects are stored in a temporary dictionary.
/// 4. OnAfterCreate() establishes relationships between objects.
/// 5. All actors are registered via IActorSystem.RegisterActor.
/// 6. Model is ready. Other letters are handled by OnModelLetter().
/// 7. On error at any stage: logging and IActorSystem.Panic().
/// </remarks>
/// <param name="system">Акторная система.</param>
/// <param name="queueCapacity">Размер очереди сообщений.</param>
public abstract class CompiledModelActor(IActorSystem system, int queueCapacity = 256)
    : Actor(system, SystemUids.Model, "model", queueCapacity)
{
    private readonly Dictionary<Guid, object> _objects = [];

    /// <summary>
    /// Запечатанный обработчик писем. <see cref="InitializeLetter"/> запускает сборку модели,
    /// остальные письма делегируются в <see cref="OnModelLetter"/>.
    /// </summary>
    /// <remarks>
    /// Sealed message handler. InitializeLetter triggers model build.
    /// Other letters are delegated to OnModelLetter.
    /// </remarks>
    protected sealed override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case InitializeLetter:
                try
                {
                    CompiledModel model = CompileModel();
                    Build(model);
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
    /// Обрабатывает все письма, кроме <see cref="InitializeLetter"/>.
    /// По умолчанию игнорирует. Наследник может переопределить.
    /// </summary>
    /// <remarks>
    /// Handles all letters except InitializeLetter. Default implementation ignores.
    /// Descendants may override.
    /// </remarks>
    protected virtual ValueTask OnModelLetter(Letter letter) => default;

    /// <summary>
    /// Компилирует модель. Наследник реализует, используя <see cref="XmlModelCompiler"/> или другой источник.
    /// </summary>
    /// <remarks>
    /// Compiles the model. Descendant implements using XmlModelCompiler or another source.
    /// </remarks>
    protected abstract CompiledModel CompileModel();

    private void Build(CompiledModel model)
    {
        // 1. Создать все объекты
        foreach (Guid uid in model.Uids)
        {
            ElementConfig? element = model.FindElement(uid);
            if (element == null)
                continue;

            var obj = CreateObject(element);
            _objects[uid] = obj;
        }

        // 2. Наследник устанавливает связи
        OnAfterCreate(model);

        // 3. Зарегистрировать акторы
        foreach (Guid uid in model.Uids)
        {
            if (_objects[uid] is Actor actor)
            {
                System.RegisterActor(actor);
            }
        }
    }

    /// <summary>
    /// Создаёт объект по конфигурации элемента.
    /// </summary>
    /// <param name="element">Конфигурация элемента из скомпилированной модели.</param>
    /// <returns>Созданный объект (актор или вспомогательный объект).</returns>
    /// <remarks>
    /// Creates an object from an element configuration.
    /// </remarks>
    protected abstract object CreateObject(ElementConfig element);

    /// <summary>
    /// Вызывается после создания всех объектов, но до регистрации акторов.
    /// Наследник устанавливает связи между объектами.
    /// </summary>
    /// <param name="model">Скомпилированная модель со всеми элементами.</param>
    /// <remarks>
    /// Called after all objects are created, but before actor registration.
    /// Descendant establishes relationships between objects.
    /// </remarks>
    protected virtual void OnAfterCreate(CompiledModel model)
    {
    }

    /// <summary>
    /// Получает ранее созданный объект по идентификатору.
    /// </summary>
    /// <typeparam name="T">Тип объекта.</typeparam>
    /// <param name="uid">Идентификатор объекта.</param>
    /// <returns>Найденный объект.</returns>
    /// <exception cref="KeyValueException">Если объект не найден.</exception>
    /// <remarks>
    /// Retrieves a previously created object by its UID.
    /// </remarks>
    protected T GetObject<T>(Guid uid) where T : class => (T)_objects[uid];

    /// <summary>
    /// Проверяет, был ли создан объект с указанным идентификатором.
    /// </summary>
    /// <param name="uid">Идентификатор объекта.</param>
    /// <returns>true, если объект существует; иначе false.</returns>
    /// <remarks>
    /// Checks whether an object with the specified UID has been created.
    /// </remarks>
    protected bool HasObject(Guid uid) => _objects.ContainsKey(uid);
}
