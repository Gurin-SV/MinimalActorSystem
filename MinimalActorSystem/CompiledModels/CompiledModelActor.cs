namespace MinimalActorSystem.CompiledModels;

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
    protected sealed override async ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case InitializeLetter:
                try
                {
                    var model = CompileModel();
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
    protected virtual ValueTask OnModelLetter(Letter letter) => default;

    /// <summary>
    /// Компилирует модель. Наследник реализует, используя <see cref="XmlModelCompiler"/> или другой источник.
    /// </summary>
    protected abstract CompiledModel CompileModel();

    private void Build(CompiledModel model)
    {
        // 1. Создать все объекты
        foreach (var uid in model.Uids)
        {
            var element = model.FindElement(uid);
            if (element == null)
                continue;

            var obj = CreateObject(element);
            _objects[uid] = obj;
        }

        // 2. Наследник устанавливает связи
        OnAfterCreate(model);

        // 3. Зарегистрировать акторы
        foreach (var uid in model.Uids)
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
    protected abstract object CreateObject(ElementConfig element);

    /// <summary>
    /// Вызывается после создания всех объектов, но до регистрации акторов.
    /// Наследник устанавливает связи между объектами.
    /// </summary>
    protected virtual void OnAfterCreate(CompiledModel model)
    {
    }

    /// <summary>
    /// Получает ранее созданный объект по идентификатору.
    /// </summary>
    protected T GetObject<T>(Guid uid) where T : class => (T)_objects[uid];

    /// <summary>
    /// Проверяет, был ли создан объект с указанным идентификатором.
    /// </summary>
    protected bool HasObject(Guid uid) => _objects.ContainsKey(uid);
}
