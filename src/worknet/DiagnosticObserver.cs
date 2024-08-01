// Copyright (C) 2015-2024 The Neo Project.
//
// DiagnosticObserver.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Diagnostics;

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
