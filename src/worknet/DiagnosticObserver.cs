// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NeoWorkNet;

using KvpObserver = IObserver<KeyValuePair<string, object>>;

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
