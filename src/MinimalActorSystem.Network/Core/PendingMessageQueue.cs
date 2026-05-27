using System.Collections.Concurrent;
using System.Linq;

namespace MinimalActorSystem.Network;

/// <summary>
/// Реализация очереди отложенных сообщений.
/// </summary>
public sealed class PendingMessageQueue : IPendingMessageQueue
{
    private readonly ConcurrentDictionary<Guid, PendingMessage> _messagesById = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<Guid>> _messagesByNode = new();

    /// <inheritdoc/>
    public int Count => _messagesById.Count;

    /// <inheritdoc/>
    public void Enqueue(PendingMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        message.IsQueued = true;
        _messagesById[message.LocalMessageId] = message;

        _messagesByNode.AddOrUpdate(
            message.DestinationNode,
            _ => [message.LocalMessageId],
            (_, bag) =>
            {
                bag.Add(message.LocalMessageId);
                return bag;
            });
    }

    /// <inheritdoc/>
    public IReadOnlyList<PendingMessage> DequeueForNode(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be empty", nameof(nodeName));

        List<PendingMessage> messages = [];

        if (_messagesByNode.TryRemove(nodeName, out ConcurrentBag<Guid>? messageIds))
        {
            foreach (Guid id in messageIds)
            {
                if (_messagesById.TryRemove(id, out PendingMessage? message))
                {
                    messages.Add(message);
                }
            }
        }

        return messages;
    }

    /// <inheritdoc/>
    public bool Remove(Guid localMessageId)
    {
        if (_messagesById.TryRemove(localMessageId, out PendingMessage? message))
        {
            if (_messagesByNode.TryGetValue(message.DestinationNode, out ConcurrentBag<Guid>? bag))
            {
                ConcurrentBag<Guid> newBag = [.. bag.Where(id => id != localMessageId)];
                if (newBag.Any())
                {
                    _messagesByNode[message.DestinationNode] = newBag;
                }
                else
                {
                    _messagesByNode.TryRemove(message.DestinationNode, out _);
                }
            }
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Contains(Guid localMessageId)
    {
        return _messagesById.ContainsKey(localMessageId);
    }

    /// <inheritdoc/>
    public PendingMessage? Get(Guid localMessageId)
    {
        return _messagesById.TryGetValue(localMessageId, out PendingMessage? message) ? message : null;
    }

    /// <inheritdoc/>
    public int GetQueuedCountForNode(string nodeName)
    {
        if (!_messagesByNode.TryGetValue(nodeName, out ConcurrentBag<Guid>? bag))
            return 0;

        return bag.Count(id => _messagesById.ContainsKey(id));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _messagesById.Clear();
        _messagesByNode.Clear();
    }
}
