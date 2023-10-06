// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;
using System;

namespace NeoExpress
{
    interface IExpressStorage : IDisposable
    {
        string Name { get; }
        IStore GetStore(string path);
    }
}
