// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;

namespace NeoExpress.Node
{
    internal class ExpressStoreProvider : IStoreProvider
    {
        public readonly IExpressStorage expressStorage;

        public ExpressStoreProvider(IExpressStorage expressStorage)
        {
            this.expressStorage = expressStorage;
        }

        public string Name => nameof(ExpressStoreProvider);
        public IStore GetStore(string path) => expressStorage.GetStore(path);
    }
}
