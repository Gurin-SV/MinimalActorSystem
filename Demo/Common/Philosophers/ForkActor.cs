using MinimalActorSystem;

namespace Demo.Common.Philosophers;

public sealed class ForkActor(IActorSystem system, Guid uid, string name)
    : Actor(system, uid, name)
{
    public bool IsTaken => _taken;

    private bool _taken;

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case StopLetter:
                _taken = false;
                break;
            case TakeForkRequestLetter:
                OnTakeForkRequest(letter);
                break;
            case PutForkRequestLetter:
                _taken = false;
                break;
        }
        return default;
    }

    private void OnTakeForkRequest(Letter letter)
    {
        if (!_taken)
        {
            _taken = true;
            System.Send(new ForkTakenResponseLetter(Uid, letter.Sender));
        }
        else
        {
            System.Send(new ForkBusyResponseLetter(Uid, letter.Sender));
        }
    }
}
