using System;
using System.Collections.Generic;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{

    public partial class ExpressSystem
    {
        class PersistenceWrapper : Plugin, IPersistencePlugin
        {
            readonly Action<Block, IReadOnlyList<ApplicationExecuted>> onPersist;
            Block? block = null;
            IReadOnlyList<ApplicationExecuted>? appExecutions = null;

            public PersistenceWrapper(Action<Block, IReadOnlyList<ApplicationExecuted>> onPersist)
            {
                this.onPersist = onPersist;
            }

            void OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
            {
                if (block is null) throw new ArgumentNullException();
                if (appExecutions is null) throw new ArgumentNullException();

                this.block = block;
                this.appExecutions = applicationExecutedList;
            }

            void OnCommit(NeoSystem system, Block block, DataCache snapshot)
            {
                if (this.block is null) throw new Exception();
                if (this.appExecutions is null) throw new Exception();

                if (block.Hash != this.block.Hash) throw new Exception();
                onPersist(this.block, this.appExecutions);
            }
        }
    }
}
