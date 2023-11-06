// Copyright (C) 2015-2023 The Neo Project.
//
// The neo is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
