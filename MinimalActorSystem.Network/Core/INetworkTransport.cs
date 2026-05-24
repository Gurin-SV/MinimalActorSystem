using System.IO;

namespace MinimalActorSystem.Network;

/// <summary>
/// Транспортный интерфейс для отправки сетевых писем между узлами.
/// Абстрагирует конкретную реализацию транспорта (HTTP, gRPC, TCP, etc.).
/// </summary>
/// <remarks>
/// Transport interface for sending network letters between nodes.
/// Abstracts the concrete transport implementation (HTTP, gRPC, TCP, etc.).
/// </remarks>
public interface INetworkTransport
{
    /// <summary>
    /// Отправляет сетевое письмо на указанный адрес.
    /// Реализует семантику "послал и забыл" - не гарантирует доставку.
    /// </summary>
    /// <param name="targetAddress">Сетевой адрес узла-получателя.</param>
    /// <param name="networkLetter">Сетевое письмо для отправки.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, представляющая асинхронную операцию отправки.</returns>
    Task SendAsync(string targetAddress, NetworkLetter networkLetter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Десериализует входящий поток данных в сетевое письмо.
    /// Вызывается приложением при получении HTTP-запроса для преобразования тела запроса в объект письма.
    /// </summary>
    /// <param name="bodyStream">Поток данных тела запроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, содержащая десериализованное сетевое письмо, или null при ошибке.</returns>
    Task<NetworkLetter?> DeserializeAsync(Stream bodyStream, CancellationToken cancellationToken = default);
}
