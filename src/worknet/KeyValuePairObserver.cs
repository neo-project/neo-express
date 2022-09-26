namespace NeoWorkNet;

public class KeyValuePairObserver : IObserver<KeyValuePair<string, object?>>
{
    readonly Action<string, object?> onNextAction;

    public KeyValuePairObserver(Action<string, object?> onNextAction)
    {
        this.onNextAction = onNextAction;
    }

    public void OnCompleted() => throw new NotSupportedException();

    public void OnError(Exception error) => throw new NotSupportedException();

    public void OnNext(KeyValuePair<string, object?> kvp)
    {
        onNextAction(kvp.Key, kvp.Value);
    }
}
