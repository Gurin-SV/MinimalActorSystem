# MinimalActorSystem.Network

Сетевое расширение для **MinimalActorSystem**, обеспечивающее распределённое взаимодействие между акторными системами на разных узлах сети.

## Архитектура

Библиотека следует принципу минимальных изменений в ядре **MinimalActorSystem**:
- **MinimalActorSystem** остаётся минимальной и локальной
- **MinimalActorSystem.Network** предоставляет "движок" распределённого взаимодействия
- Конкретная транспортная реализация (веб-серверы, шлюзы, безопасность) остаётся за разработчиком приложения

## Ключевые компоненты

### 1. Proxy-актор (`NetworkProxyActor`)
Единственный на каждом узле, принимает письма с идентификатором `SystemUids.Network` и отвечает за:
- Сериализацию/десериализацию писем
- Разрешение имён узлов в сетевые адреса
- Отправку писем через транспортный уровень
- Управление очередью отложенных писем

### 2. Сетевые письма (`NetworkLetter`)
Сериализуемые сообщения, содержащие:
- `ReceiverNodeName` — символическое имя узла-получателя
- `SerializedLetter` — JSON-сериализованное оригинальное письмо
- `LetterTypeName` — тип оригинального письма для десериализации

### 3. Клиент маршрутизатора (`IRouterClient`)
Интерфейс для взаимодействия с серверами-маршрутизаторами:
- Регистрация узла в сети
- Получение таблицы разрешения имён
- Подтверждение доступности (heartbeat)

### 4. Транспорт (`INetworkTransport`)
Абстракция транспортного уровня для отправки/приёма сетевых писем.
Базовая реализация `HttpNetworkTransport` использует HTTP/REST.

## Топология сети

```
┌─────────────────┐         ┌─────────────────┐
│     Node A      │         │     Node B      │
│  ┌───────────┐  │         │  ┌───────────┐  │
│  │  Actors   │  │         │  │  Actors   │  │
│  └─────┬─────┘  │         │  └─────┬─────┘  │
│        │        │         │        │        │
│  ┌─────▼─────┐  │         │  ┌─────▼─────┐  │
│  │  Network  │◄─┼─────────┼─►│  Network  │  │
│  │   Proxy   │  │         │  │   Proxy   │  │
│  └─────┬─────┘  │         │  └─────┬─────┘  │
│        │        │         │        │        │
│  ┌─────▼─────┐  │         │  ┌─────▼─────┐  │
│  │  Router   │  │         │  │  Router   │  │
│  │  Client   │  │         │  │  Client   │  │
│  └─────┬─────┘  │         │  └─────┬─────┘  │
└────────┼────────┘         └────────┼────────┘
         │                           │
         └───────────┬───────────────┘
                     │
         ┌───────────▼───────────┐
         │    Router Server      │
         │  (DNS-like service)   │
         └───────────────────────┘
```

## Использование

### 1. Создание сетевого proxy-актора

```csharp
public class MyNetworkProxyActor : NetworkProxyActor
{
    private readonly INetworkTransport _transport;
    private readonly IRouterClient _routerClient;

    public MyNetworkProxyActor(
        IActorSystem system,
        INetworkTransport transport,
        IRouterClient routerClient)
        : base(system, SystemUids.Network, "NetworkProxy")
    {
        _transport = transport;
        _routerClient = routerClient;
    }

    protected override async Task SendToNetwork(Letter letter, string receiverNodeName)
    {
        // Сериализуем письмо
        var serialized = SerializeLetter(letter);
        var letterTypeName = letter.GetType().FullName!;

        // Создаём сетевое письмо
        var networkLetter = new DefaultNetworkLetter(
            receiverNodeName,
            serialized,
            letterTypeName
        );

        // Получаем адрес узла из таблицы разрешения
        if (!NodeResolutionTable.TryGetValue(receiverNodeName, out var targetAddress))
        {
            // Адрес неизвестен - ставим в очередь
            EnqueuePending(letter);
            return;
        }

        // Отправляем через транспорт
        await _transport.SendAsync(targetAddress, networkLetter, System.CancellationToken);
    }

    protected override async ValueTask HandleLocalLetter(Letter letter)
    {
        // Обработка специальных локальных писем
        if (letter is ResolutionTableUpdateLetter updateLetter)
        {
            UpdateResolutionTable(updateLetter.ResolutionTable);
            await FlushPendingLetters();
        }
    }
}
```

### 2. Настройка сети

```csharp
var settings = new NetworkSettings
{
    NodeName = "NodeA",
    RouterAddresses = new[]
    {
        "http://router1.example.com:5000",
        "http://router2.example.com:5000"
    },
    RouterTimeoutMs = 5000,
    NetworkResponseTimeoutMs = 10000
};

var actorSystem = new ActorSystem(settings);
```

### 3. Отправка письма на другой узел

```csharp
// Письмо должно включать свойство ReceiverNodeName
public sealed class RemoteCommandLetter : Letter, INetworkLetterReceiver
{
    public RemoteCommandLetter(Guid sender, Guid receiver, string receiverNodeName, string command)
        : base(sender, receiver)
    {
        ReceiverNodeName = receiverNodeName;
        Command = command;
    }

    public string ReceiverNodeName { get; }
    public string Command { get; }
}

// Отправка
var letter = new RemoteCommandLetter(
    senderUid,
    targetActorUid,
    "NodeB",  // Имя узла-получателя
    "ExecuteSomething"
);

actorSystem.Send(letter);  // Письмо будет отправлено через NetworkProxyActor
```

## Интеграция с приложением

Приложение должно предоставить:

1. **Реализацию веб-сервиса маршрутизатора** — для разрешения имён узлов и мониторинга доступности
2. **Реализацию веб-сервиса узла** — для приёма входящих сетевых писем (через `INetworkTransport.ListenAsync`)
3. **Конкретную реализацию транспорта** — если HTTP/REST не подходит (gRPC, TCP, etc.)

## Требования

- .NET Standard 2.1
- [MinimalActorSystem](https://www.nuget.org/packages/MinimalActorSystem)
- System.Text.Json
- Microsoft.Extensions.Http

## Лицензия

MIT