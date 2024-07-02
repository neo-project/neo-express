// Copyright (C) 2015-2024 The Neo Project.
//
// DelegateDisposable.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
