using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Network.Tests.Actors;

/// <summary>
/// Тестовый актор для проверки доставки сообщений через NetworkActor.
/// Сохраняет полученные письма для последующей проверки в тестах.
/// </summary>
/// <remarks>
/// Test actor for verifying message delivery through NetworkActor.
/// Stores received letters for later verification in tests.
/// </remarks>
/// <remarks>
/// Создаёт новый экземпляр тестового актора-получателя.
/// </remarks>
/// <param name="system">Акторная система.</param>
/// <param name="uid">Уникальный идентификатор актора.</param>
/// <param name="name">Имя актора.</param>
/// <param name="queueCapacity">Размер очереди сообщений.</param>
public sealed class TestReceiverActor(IActorSystem system, Guid uid, string name, int queueCapacity = 64)
    : Actor(system, uid, name, queueCapacity)
{
    private readonly List<IPayloadLetter> _receivedLetters = [];
    private readonly TaskCompletionSource<IPayloadLetter> _firstLetterTcs = new();
    private readonly object _lock = new();

    /// <summary>
    /// Все полученные письма.
    /// </summary>
    public IReadOnlyList<IPayloadLetter> ReceivedLetters => _receivedLetters;

    /// <summary>
    /// Задача, которая завершается при получении первого письма.
    /// </summary>
    public Task<IPayloadLetter> FirstLetterReceived => _firstLetterTcs.Task;

    /// <summary>
    /// Обрабатывает входящее письмо.
    /// </summary>
    protected override ValueTask OnLetter(Letter letter)
    {
        if (letter is IPayloadLetter payloadLetter)
        {
            lock (_lock)
            {
                _receivedLetters.Add(payloadLetter);

                if (_receivedLetters.Count == 1)
                {
                    _firstLetterTcs.TrySetResult(payloadLetter);
                }
            }

            System.Logger.LogDebug("TestReceiverActor {Name} received letter with payload type {PayloadType}",
                Name, payloadLetter.PayloadType.Name);
        }
        else
        {
            System.Logger.LogWarning("TestReceiverActor {Name} received non-IPayloadLetter: {LetterType}",
                Name, letter.GetType().Name);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Сбрасывает состояние актора (очищает полученные письма).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _receivedLetters.Clear();
        }
    }
}
