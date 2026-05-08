namespace MinimalActorSystem.CompiledModels;

/// <summary>
/// Модельный актор, собирающий систему из <see cref="CompiledModel"/>.
/// Альтернатива <see cref="ModelActor"/> для декларативного построения модели из XML.
/// В одной акторной системе может существовать только один модельный актор.
/// 
/// Порядок сборки:
/// 1. Создаются все объекты (и акторы, и вспомогательные) во временный словарь.
/// 2. Для каждого объекта устанавливаются связи с источниками.
/// 3. Акторы регистрируются в системе, объекты остаются в локальном словаре.
/// </summary>
/// <remarks>
/// Создаёт модельный актор. Uid фиксирован — <see cref="SystemUids.Model"/>.
/// </remarks>
/// <param name="system">Акторная система.</param>
/// <param name="queueCapacity">Размер очереди сообщений.</param>
public abstract class CompiledModelActor(IActorSystem system, int queueCapacity = 256) 
    : Actor(system, SystemUids.Model, "model", queueCapacity)
{
    private readonly Dictionary<Guid, object> _objects = [];

    /// <summary>
    /// Запускает построение модели из скомпилированного графа.
    /// Вызывается после регистрации модельного актора в системе.
    /// </summary>
    /// <param name="model">Скомпилированная модель.</param>
    protected void Build(CompiledModel model)
    {
        try
        {
            OnBuild(model);
        }
        catch (Exception ex)
        {
            System.Logger.LogError(ex, "Model build failed, triggering panic");
            System.Panic();
        }
    }

    private void OnBuild(CompiledModel model)
    {
        // 1. Создать все объекты во временный словарь
        foreach (var uid in model.Uids)
        {
            var element = model.FindElement(uid);
            if (element == null)
                continue;

            var obj = CreateObject(element);
            _objects[uid] = obj;
        }

        // 2. Установить связи с источниками
        foreach (var uid in model.Uids)
        {
            var element = model.FindElement(uid);
            if (element?.SourceUid == null)
                continue;

            var consumer = _objects[uid];
            var source = _objects[element.SourceUid.Value];
            BindSource(source, consumer);
        }

        // 3. Зарегистрировать акторы в системе
        foreach (var uid in model.Uids)
        {
            if (_objects[uid] is Actor actor)
            {
                System.RegisterActor(actor);
            }
        }

        // 4. Оповестить акторы о завершении сборки
        foreach (var uid in model.Uids)
        {
            if (_objects[uid] is Actor actor)
            {
                OnActorBuilt(actor, model.FindElement(uid)!);
            }
        }
    }

    /// <summary>
    /// Создаёт объект по конфигурации элемента. Объект может быть актором или вспомогательным объектом.
    /// На этом этапе связи ещё не установлены.
    /// </summary>
    /// <param name="element">Конфигурация элемента.</param>
    /// <returns>Созданный объект (актор или вспомогательный объект).</returns>
    protected abstract object CreateObject(ElementConfig element);

    /// <summary>
    /// Устанавливает связь между источником и потребителем.
    /// Поток данных направлен от источника к потребителю.
    /// Один источник может иметь много потребителей.
    /// </summary>
    /// <param name="source">Объект-источник.</param>
    /// <param name="consumer">Объект-потребитель.</param>
    protected abstract void BindSource(object source, object consumer);

    /// <summary>
    /// Вызывается после того, как актор зарегистрирован в системе и все связи установлены.
    /// Актор может выполнить финальную инициализацию, зная полную конфигурацию и источники.
    /// </summary>
    /// <param name="actor">Актор.</param>
    /// <param name="element">Конфигурация элемента актора.</param>
    protected virtual void OnActorBuilt(Actor actor, ElementConfig element)
    {
        // По умолчанию — ничего не делаем
    }

    /// <summary>
    /// Получает ранее созданный объект по идентификатору.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип объекта.</typeparam>
    /// <param name="uid">Идентификатор объекта.</param>
    /// <returns>Объект запрошенного типа.</returns>
    /// <exception cref="KeyNotFoundException">Объект с указанным Uid не найден.</exception>
    /// <exception cref="InvalidCastException">Объект не совместим с запрошенным типом.</exception>
    protected T GetObject<T>(Guid uid) where T : class => (T)_objects[uid];

    /// <summary>
    /// Проверяет, был ли создан объект с указанным идентификатором.
    /// </summary>
    /// <param name="uid">Идентификатор объекта.</param>
    /// <returns><c>true</c>, если объект существует.</returns>
    protected bool HasObject(Guid uid) => _objects.ContainsKey(uid);
}
