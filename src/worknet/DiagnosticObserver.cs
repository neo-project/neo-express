using System.Diagnostics;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;

namespace NeoWorkNet;

using KvpObserver = IObserver<KeyValuePair<string, object?>>;

class DiagnosticObserver : IObserver<DiagnosticListener>
{
    readonly IReadOnlyDictionary<string, KvpObserver> observers;

    public DiagnosticObserver(string name, KvpObserver observer)
    {
        observers = new Dictionary<string, KvpObserver>() { { name, observer } };
    }

    public void OnCompleted() => throw new NotSupportedException();

    public void OnError(Exception error) => throw new NotSupportedException();

    public void OnNext(DiagnosticListener value)
    {
        if (observers.TryGetValue(value.Name, out var observer))
        {
            value.Subscribe(observer);
        }
    }
}
