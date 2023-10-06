// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System;

namespace Neo.BlockchainToolkit.Utilities
{
    class DelegateDisposable : IDisposable
    {
        readonly Action action;
        bool disposed = false;

        public DelegateDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                action();
                disposed = true;
            }
        }
    }
}
