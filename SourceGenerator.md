# SourceGenerator: автоматическая диспетчеризация писем

## Назначение

Source generator `ActorLetterHandlerGenerator` избавляет от ручного написания метода `OnLetter` со `switch` в наследниках `Actor`. Генератор сам находит обработчики писем в классе и создаёт диспетчеризацию на этапе компиляции.

## Подключение

Добавить ссылку на проект генератора в csproj:

    <ItemGroup>
      <ProjectReference Include="..\MinimalActorSystem.SourceGenerator\MinimalActorSystem.SourceGenerator.csproj"
                        OutputItemType="Analyzer"
                        ReferenceOutputAssembly="false" />
    </ItemGroup>

Ядро `MinimalActorSystem` должно быть подключено как обычно.

## Использование

### 1. Пометить актор атрибутом

Класс актора должен быть `partial` и помечен атрибутом `[ActorLetterHandler]`:

    [ActorLetterHandler]
    public partial class MyActor : Actor
    {
        // ...
    }

### 2. Создать обработчики писем

Для каждого типа письма, которое актор должен обрабатывать, создаётся private-метод с сигнатурой:

    private ValueTask On{ИмяТипаПисьма}({ИмяТипаПисьма} letter)

Имя метода жёстко привязано к имени типа письма: для типа `Ping` метод должен называться `OnPing`, для `SensorDataLetter` — `OnSensorDataLetter`, и т.д.

Пример:

    [ActorLetterHandler]
    public partial class PingActor : Actor
    {
        public PingActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name) { }

        private ValueTask OnPing(Ping ping)
        {
            // обработка Ping
            return default;
        }

        private ValueTask OnPong(Pong pong)
        {
            // обработка Pong
            return default;
        }
    }

### 3. Сгенерированный код

Генератор автоматически создаст partial-дополнение класса с переопределением `OnLetter`:

    public partial class PingActor
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case Ping msg:
                    return OnPing(msg);
                case Pong msg:
                    return OnPong(msg);
                default:
                    return default;
            }
        }
    }

Никакого другого кода в `OnLetter` не требуется — только диспетчеризация. Вся бизнес-логика остаётся в отдельных методах-обработчиках.

## Ограничения

- **Тип письма должен быть `sealed`.** Это требование манифеста и проверяется генератором.
- **Тип письма должен наследовать от `Letter`.** Проверяется восхождением по цепочке базовых классов.
- **Имя метода строго `On{ИмяТипа}`.** IntelliSense в Visual Studio требует этой конвенции для распознавания реализации абстрактного метода. Без неё IDE показывает ошибку, хотя компиляция проходит.
- **Методы должны быть `private`.** Генератор игнорирует методы с другими модификаторами доступа.
- **Методы должны возвращать `ValueTask`.** Сигнатура зафиксирована: ровно один параметр — sealed-письмо, возвращаемый тип — `ValueTask`.

## Как это работает

1. На этапе компиляции генератор сканирует все partial-классы с атрибутом `[ActorLetterHandler]`.
2. Для каждого класса собирает private-методы, удовлетворяющие сигнатуре: `ValueTask On{Type}({Type} letter)`, где `{Type}` — sealed-класс, наследующий `Letter`.
3. Генерирует partial-дополнение с `OnLetter`, содержащим `switch` по всем найденным типам.
4. Компилятор объединяет исходный и сгенерированный partial в один класс.

## Состав

- **`MinimalActorSystem`** — атрибут `ActorLetterHandlerAttribute` (пустой, маркерный).
- **`MinimalActorSystem.SourceGenerator`** — генератор `ActorLetterHandlerGenerator`.
- **Проект-потребитель** — partial-акторы с атрибутом и методами-обработчиками.