using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug;
using NeoDebug.Models;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace NeoExpress.Node
{
    internal class StorageServiceContainer : IVariableContainer
    {
        internal class StorageItemContainer : IVariableContainer
        {
            private readonly IVariableContainerSession session;
            private readonly Neo.Ledger.StorageKey key;
            private readonly Neo.Ledger.StorageItem item;

            public StorageItemContainer(IVariableContainerSession session, KeyValuePair<Neo.Ledger.StorageKey, Neo.Ledger.StorageItem> kvp)
            {
                this.session = session;
                key = kvp.Key;
                item = kvp.Value;
            }

            public IEnumerable<Variable> GetVariables(VariablesArguments args)
            {
                yield return ByteArrayContainer.GetVariable(key.Key, session, "key");
                yield return ByteArrayContainer.GetVariable(item.Value, session, "value");
                yield return new Variable()
                {
                    Name = "constant",
                    Value = item.IsConstant.ToString(),
                    Type = "Boolean"
                };
            }
        }

        private readonly Neo.UInt160 scriptHash;
        private readonly Neo.Persistence.Snapshot snapshot;
        private readonly IVariableContainerSession session;

        public StorageServiceContainer(IVariableContainerSession session, Neo.Persistence.Snapshot snapshot, Neo.UInt160 scriptHash)
        {
            this.session = session;
            this.snapshot = snapshot;
            this.scriptHash = scriptHash;
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            foreach (var kvp in snapshot.Storages.Find())
            {
                if (kvp.Key.ScriptHash == scriptHash)
                {
                    yield return new Variable()
                    {
                        Name = "0x" + new BigInteger(kvp.Key.Key).ToString("x"),
                        VariablesReference = session.AddVariableContainer(new StorageItemContainer(session, kvp)),
                        NamedVariables = 3
                    };
                }
            }
        }
    }
}
