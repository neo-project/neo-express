using Neo;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using NeoExpress.Neo3.Node;
using Nito.Disposables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Neo3
{
    using StackItemType = Neo.VM.Types.StackItemType;

    public class BlockchainOperations
    {
        public ExpressChain CreateBlockchain(FileInfo output, int count, TextWriter writer)
        {
            if (File.Exists(output.FullName))
            {
                throw new ArgumentException($"{output.FullName} already exists", nameof(output));
            }

            if (count != 1 && count != 4 && count != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(count));
            }

            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(49000 + (index * 1000) + portNumber);

            for (var i = 1; i <= count; i++)
            {
                var wallet = new DevWallet($"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

            var contract = Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                multiSigContractAccount.Label = "MultiSigContract";
            }

            // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
            // http://www.iana.org/assignments/port-numbers
            var nodes = new List<ExpressConsensusNode>(count);
            for (var i = 0; i < count; i++)
            {
                nodes.Add(new ExpressConsensusNode()
                {
                    TcpPort = GetPortNumber(i, 333),
                    WebSocketPort = GetPortNumber(i, 334),
                    RpcPort = GetPortNumber(i, 332),
                    Wallet = wallets[i].wallet.ToExpressWallet()
                });
            }

            writer.WriteLine($"Created {count} node privatenet at {output.FullName}");
            writer.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
            writer.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

            return new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };
        }

        public byte[] ToScriptHashByteArray(ExpressWalletAccount account)
        {
            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);
            return devAccount.ScriptHash.ToArray();
        }

        public void ResetNode(ExpressChain chain, int index)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            if (node.IsRunning())
            {
                throw new Exception($"node {index} currently running");
            }

            var folder = node.GetBlockchainPath();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        private const string GENESIS = "genesis";

        public ExpressWallet CreateWallet(ExpressChain chain, string name)
        {
            if (IsReservedName())
            {
                throw new Exception($"{name} is a reserved name. Choose a different wallet name.");
            }

            var wallet = new DevWallet(name);
            var account = wallet.CreateAccount();
            account.IsDefault = true;
            return wallet.ToExpressWallet();

            bool IsReservedName()
            {
                if (string.Equals(GENESIS, name, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                foreach (var node in chain.ConsensusNodes)
                {
                    if (string.Equals(node.Wallet.Name, name, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        public async Task RunBlockchainAsync(ExpressChain chain, int index, uint secondsPerBlock, bool discard, bool enableTrace, TextWriter writer, CancellationToken cancellationToken)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            if (node.IsRunning())
            {
                throw new Exception($"node {index} already running");
            }

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var folder = node.GetBlockchainPath();
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            writer.WriteLine(folder);

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            using var runningMutex = node.CreateRunningMutex();
            using var store = GetStore();
            await NodeUtility.RunAsync(store, node, enableTrace, writer, cancellationToken).ConfigureAwait(false);

            Neo.Persistence.IStore GetStore()
            {
                if (discard)
                {
                    try
                    {
                        var rocksDbStore = RocksDbStore.OpenReadOnly(folder);
                        return new CheckpointStore(rocksDbStore);
                    }
                    catch
                    {
                        return new CheckpointStore(new NullReadOnlyStore());
                    }
                }
                else
                {
                    return RocksDbStore.Open(folder);
                }
            }
        }

        class NullReadOnlyStore : Neo.Persistence.IReadOnlyStore
        {
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] key, SeekDirection direction)
                => Enumerable.Empty<(byte[] Key, byte[] Value)>();

            public byte[]? TryGet(byte table, byte[]? key) => null;
        }

        public async Task RunCheckpointAsync(ExpressChain chain, string checkPointArchive, uint secondsPerBlock, bool enableTrace, TextWriter writer, CancellationToken cancellationToken)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            if (node.IsRunning())
            {
                throw new Exception($"checkpoint node already running");
            }

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            string checkpointTempPath;
            do
            {
                checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(checkpointTempPath));

            using var folderCleanup = AnonymousDisposable.Create(() =>
            {
                if (Directory.Exists(checkpointTempPath))
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            });

            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
            RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            using var runningMutex = node.CreateRunningMutex();
            using var rocksDbStore = RocksDbStore.OpenReadOnly(checkpointTempPath);
            using var checkpointStore = new CheckpointStore(rocksDbStore);
            await NodeUtility.RunAsync(checkpointStore, node, enableTrace, writer, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateCheckpoint(ExpressChain chain, string checkPointFileName, TextWriter writer)
        {
            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("Checkpoint file already exists", nameof(checkPointFileName));
            }

            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            var folder = node.GetBlockchainPath();

            if (node.IsRunning())
            {
                var uri = chain.GetUri();
                var rpcClient = new RpcClient(uri.ToString());
                await rpcClient.RpcSendAsync("expresscreatecheckpoint", checkPointFileName).ConfigureAwait(false);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint online");
            }
            else
            {
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                using var db = RocksDbStore.Open(folder);
                db.CreateCheckpoint(checkPointFileName, chain.Magic, multiSigAccount.ScriptHash);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint offline");
            }
        }

        private const string CHECKPOINT_EXTENSION = ".nxp3-checkpoint";

        public string ResolveCheckpointFileName(string checkPointFileName)
        {
            checkPointFileName = string.IsNullOrEmpty(checkPointFileName)
                ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}{CHECKPOINT_EXTENSION}"
                : checkPointFileName;

            if (!Path.GetExtension(checkPointFileName).Equals(CHECKPOINT_EXTENSION))
            {
                checkPointFileName += CHECKPOINT_EXTENSION;
            }

            return Path.GetFullPath(checkPointFileName);
        }

        public void RestoreCheckpoint(ExpressChain chain, string checkPointArchive, bool force)
        {
            string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                var node = chain.ConsensusNodes[0];
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                var blockchainDataPath = node.GetBlockchainPath();

                if (!force && Directory.Exists(blockchainDataPath))
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);

                if (Directory.Exists(blockchainDataPath))
                {
                    Directory.Delete(blockchainDataPath, true);
                }

                Directory.Move(checkpointTempPath, blockchainDataPath);
            }
            finally
            {
                if (Directory.Exists(checkpointTempPath))
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            }
        }

        // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/transaction.md
        public async Task<UInt256> Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var expressNode = chain.GetExpressNode();
            var assetHash = NodeUtility.GetAssetId(asset);
            var senderHash = sender.GetScriptHashAsUInt160();
            var receiverHash = receiver.GetScriptHashAsUInt160();

            if ("all".Equals(quantity, StringComparison.InvariantCultureIgnoreCase))
            {
                using var sb = new ScriptBuilder();
                sb.EmitAppCall(assetHash, "balanceOf", senderHash);
                // current balance on eval stack after balanceOf call
                sb.EmitPush(receiverHash);
                sb.EmitPush(senderHash);
                sb.EmitPush(3);
                sb.Emit(OpCode.PACK);
                sb.EmitPush("transfer");
                sb.EmitPush(assetHash);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return await expressNode.Execute(chain, sender, sb.ToArray()).ConfigureAwait(false);
            }
            else if(decimal.TryParse(quantity, out var amount))
            {
                var (_, results) = await expressNode.Invoke(assetHash.MakeScript("decimals")).ConfigureAwait(false);
                if (results.Length > 0 && results[0].Type == StackItemType.Integer)
                {
                    var decimals = (byte)results[0].GetInteger();
                    var value = amount.ToBigInteger(decimals);
                    return await expressNode.Execute(chain, sender, assetHash.MakeScript("transfer", senderHash, receiverHash, value)).ConfigureAwait(false);
                }
                else
                {
                    throw new Exception("Invalid response from decimals operation");
                }
            }
            else
            {
                throw new Exception($"Invalid quantity value {quantity}");
            }
        }

        // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/contract.md
        // https://github.com/ProDog/NEO-Test/blob/master/RpcClientTest/Test_ContractClient.cs#L38
        public async Task<UInt256> DeployContract(ExpressChain chain, string contract, ExpressWalletAccount account)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var expressNode = chain.GetExpressNode();
            var accountHash = account.GetScriptHashAsUInt160();
            var (nefFile, manifest) = await LoadContract(contract).ConfigureAwait(false);

            using var sb = new ScriptBuilder();
            sb.EmitSysCall(ApplicationEngine.System_Contract_Create, nefFile.Script, manifest.ToString());
            return await expressNode.Execute(chain, account, sb.ToArray()).ConfigureAwait(false);

            static async Task<(NefFile nefFile, ContractManifest manifest)> LoadContract(string contractPath)
            {
                var nefTask = Task.Run(() =>
                {
                    using var stream = File.OpenRead(contractPath);
                    using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                    return reader.ReadSerializable<NefFile>();
                });

                var manifestTask = File.ReadAllBytesAsync(Path.ChangeExtension(contractPath, ".manifest.json"))
                    .ContinueWith(t => ContractManifest.Parse(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);

                await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
                return (nefTask.Result, manifestTask.Result);
            }
        }

        public async Task<UInt256> InvokeContract(ExpressChain chain, string invocationFilePath, ExpressWalletAccount account, decimal additionalGas = 0m)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var expressNode = chain.GetExpressNode();
            var script = await ContractParameterParser.LoadInvocationScript(invocationFilePath).ConfigureAwait(false);
            return await expressNode.Execute(chain, account, script, additionalGas).ConfigureAwait(false);
        }

        public async Task<(BigDecimal gasConsumed, Neo.VM.Types.StackItem[] results)> TestInvokeContract(ExpressChain chain, string invocationFilePath)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var expressNode = chain.GetExpressNode();
            var script = await ContractParameterParser.LoadInvocationScript(invocationFilePath).ConfigureAwait(false);
            return await expressNode.Invoke(script).ConfigureAwait(false);
        }

        public ExpressWalletAccount? GetAccount(ExpressChain chain, string name)
        {
            var wallet = (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => name.Equals(w.Name, StringComparison.InvariantCultureIgnoreCase));
            if (wallet != null)
            {
                return wallet.DefaultAccount;
            }

            var node = chain.ConsensusNodes
                .SingleOrDefault(n => name.Equals(n.Wallet.Name, StringComparison.InvariantCultureIgnoreCase));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if (GENESIS.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.IsMultiSigContract()))
                    .FirstOrDefault();
            }

            return null;
        }

        public async Task<BigDecimal> ShowBalance(ExpressChain chain, ExpressWalletAccount account, string asset)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var assetHash = NodeUtility.GetAssetId(asset);
            var accountHash = account.GetScriptHashAsUInt160();
            var expressNode = chain.GetExpressNode();

            using var sb = new ScriptBuilder();
            sb.EmitAppCall(assetHash, "balanceOf", accountHash);
            sb.EmitAppCall(assetHash, "decimals");

            var (_, results) = await expressNode.Invoke(sb.ToArray()).ConfigureAwait(false);
            if (results.Length >= 2
                && results[0].Type == StackItemType.Integer
                && results[1].Type == StackItemType.Integer)
            {
                var value = results[0].GetInteger();
                var decimals = (byte)results[1].GetInteger();
                return new BigDecimal(value, decimals);
            }

            throw new Exception("invalid script results");
        }

        // TODO: Return RpcApplicationLog once https://github.com/neo-project/neo-modules/issues/324 is fixed
        public async Task<(RpcTransaction tx, Neo.IO.Json.JObject? log)> ShowTransaction(ExpressChain chain, string txHash)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var tx = rpcClient.GetRawTransaction(txHash);
            try
            {
                var log = await rpcClient.RpcSendAsync("getapplicationlog", txHash).ConfigureAwait(false);
                return (tx, log);
            }
            catch
            {
                return (tx, null);
            }
        }

        public Task<RpcBlock> ShowBlock(ExpressChain chain, string blockHash)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var result = rpcClient.GetBlock(blockHash);
            return Task.FromResult(result);
        }

        public async Task<IReadOnlyList<ExpressStorage>> GetStorages(ExpressChain chain, string hashOrContract)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var scriptHash = ParseScriptHash(hashOrContract);
            var json = await Task.Run(() => rpcClient.RpcSend("expressgetcontractstorage", scriptHash.ToString()))
                .ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                var storages = new List<ExpressStorage>(array.Count);
                foreach (var s in array)
                {
                    var storage = new ExpressStorage()
                    {
                        Key = s["key"].AsString(),
                        Value = s["value"].AsString(),
                        Constant = s["constant"].AsBoolean()
                    };
                    storages.Add(storage);
                }
                return storages.AsReadOnly();
            }

            return new List<ExpressStorage>(0).AsReadOnly();
        }

        public async Task<ContractManifest> GetContract(ExpressChain chain, string hashOrContract)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var scriptHash = ParseScriptHash(hashOrContract);
            var contractState = await Task.Run(() => rpcClient.GetContractState(scriptHash.ToString()))
                .ConfigureAwait(false);

            return contractState.Manifest;
        }

        public async Task<IReadOnlyList<ContractManifest>> ListContracts(ExpressChain chain)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var json = await Task.Run(() => rpcClient.RpcSend("expresslistcontracts"))
                .ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                var contracts = new List<ContractManifest>(array.Count);
                foreach (var c in array)
                {
                    contracts.Add(ContractManifest.FromJson(c));
                }
                return contracts;
            }

            return new List<ContractManifest>(0).AsReadOnly();
        }

        static UInt160 ParseScriptHash(string hashOrContract)
        {
            if (UInt160.TryParse(hashOrContract, out var hash))
            {
                return hash;
            }

            if (File.Exists(hashOrContract))
            {
                using var stream = File.OpenRead(hashOrContract);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                var nefFile = reader.ReadSerializable<NefFile>();
                return nefFile.ScriptHash;
            }

            throw new ArgumentException(nameof(hashOrContract));
        }
    }
}
