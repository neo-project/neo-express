// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;
using System.Collections.Generic;

namespace NeoWorkNet;

public class KeyValuePairObserver : IObserver<KeyValuePair<string, object>>
{
    readonly Action<string, object> onNextAction;

    public KeyValuePairObserver(Action<string, object> onNextAction)
    {
        this.onNextAction = onNextAction;
    }

    public void OnCompleted() => throw new NotSupportedException();

    public void OnError(Exception error) => throw new NotSupportedException();

    public void OnNext(KeyValuePair<string, object> kvp)
    {
        onNextAction(kvp.Key, kvp.Value);
    }
}
