using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using NeoExpress.Neo3.Node;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Neo3
{
    public class BlockchainOperations
    {
        public ExpressChain CreateBlockchain(FileInfo output, int count, uint preloadGas, TextWriter writer, CancellationToken token = default)
        {
            if (File.Exists(output.FullName))
            {
                throw new ArgumentException($"{output.FullName} already exists", nameof(output));
            }

            if (count != 1 && count != 4 && count != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(count));
            }

            // TODO: remove this restriction
            if (preloadGas > 0 && count != 1)
            {
                throw new ArgumentException("gas can only be preloaded on a single node blockchain", nameof(preloadGas));
            }

            var chain = BlockchainOperations.CreateBlockchain(count);

            writer.WriteLine($"Created {count} node privatenet at {output.FullName}");
            writer.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
            writer.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

            // if (preloadGas > 0)
            // {
            //     var node = chain.ConsensusNodes[0];
            //     var folder = node.GetBlockchainPath();

            //     if (!Directory.Exists(folder))
            //     {
            //         Directory.CreateDirectory(folder);
            //     }

            //     if (!NodeUtility.InitializeProtocolSettings(chain))
            //     {
            //         throw new Exception("could not initialize protocol settings");
            //     }

            //     using var store = new RocksDbStore(folder);
            //     NodeUtility.Preload(preloadGas, store, node, writer, token);
            // }

            return chain;

        }

        static ExpressChain CreateBlockchain(int count)
        {
            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            ushort GetPortNumber(int index, ushort portNumber) => (ushort)((49000 + (index * 1000)) + portNumber);

            for (var i = 1; i <= count; i++)
            {
                var wallet = new DevWallet($"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

            var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

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

            return new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };
        }

        private const string GENESIS = "genesis";

        static bool EqualsIgnoreCase(string a, string b)
            => string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);

        public ExpressWallet CreateWallet(ExpressChain chain, string name)
        {
            bool IsReservedName()
            {
                if (EqualsIgnoreCase(GENESIS, name)) 
                    return true;

                foreach (var node in chain.ConsensusNodes)
                {
                    if (EqualsIgnoreCase(name, node.Wallet.Name))
                        return true;
                }

                return false;
            }

            if (IsReservedName())
            {
                throw new Exception($"{name} is a reserved name. Choose a different wallet name.");
            }

            var wallet = new DevWallet(name);
            var account = wallet.CreateAccount();
            account.IsDefault = true;
            return wallet.ToExpressWallet();
        }

        public Task RunBlockchainAsync(ExpressChain chain, int index, uint secondsPerBlock, bool reset, TextWriter writer, CancellationToken cancellationToken)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            var folder = node.GetBlockchainPath();

            if (reset && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            writer.WriteLine(folder);

            var wallet = DevWallet.FromExpressWallet(node.Wallet);
            var account = wallet.GetAccounts().Single(a => a.IsMultiSigContract());

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            using var mutex = new Mutex(true, account.Address);

            var storagePlugin = new RocksDbStoragePlugin(folder);
            return NodeUtility.RunAsync(storagePlugin.Name, node, writer, cancellationToken);
        }

        public Task RunCheckpointAsync(ExpressChain chain, int index, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            var folder = node.GetBlockchainPath();

            if (!Directory.Exists(folder))
            {
                throw new Exception("invalid checkpoint");
            }

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            writer.WriteLine(folder);

            var wallet = DevWallet.FromExpressWallet(node.Wallet);
            var account = wallet.GetAccounts().Single(a => a.IsMultiSigContract());

            var storagePlugin = new CheckpointStoragePlugin(folder);
            return NodeUtility.RunAsync(storagePlugin.Name, node, writer, cancellationToken);
        }

        static IEnumerable<ExpressWalletAccount> GetMultiSigAccounts(ExpressChain chain, string scriptHash)
        {
            return chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.FirstOrDefault(a => a.ScriptHash == scriptHash))
                .Where(a => a != null);
        }

        static void AddSignatures(ExpressChain chain, TransactionManager tm, WalletAccount account)
        {
            IEnumerable<WalletAccount> GetMultiSigAccounts()
            {
                var scriptHash = Neo.Wallets.Helper.ToAddress(account.ScriptHash);
                return chain.ConsensusNodes
                    .Select(n => n.Wallet)
                    .Concat(chain.Wallets)
                    .Select(w => w.Accounts.FirstOrDefault(a => a.ScriptHash == scriptHash))
                    .Where(a => a != null)
                    .Select(DevWalletAccount.FromExpressWalletAccount);
            }

            if (account.IsMultiSigContract())
            {
                var signers = GetMultiSigAccounts();

                var publicKeys = signers.Select(s => s.GetKey()!.PublicKey).ToArray();
                var sigCount = account.Contract.ParameterList.Length;

                foreach (var signer in signers.Take(sigCount))
                {
                    var keyPair = signer.GetKey() ?? throw new Exception();
                    tm = tm.AddMultiSig(keyPair, sigCount, publicKeys);
                }
            }
            else
            {
                tm = tm.AddSignature(account.GetKey()!);
            }
        }

        public async Task<UInt256> Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            // TODO: remove once RpcClient provides async methods 
            await Task.CompletedTask;
            
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var assetHash = NodeUtility.GetAssetId(asset);
            var amount = GetAmount();

            // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/transaction.md#constructing-a-transaction-to-transfer-from-multi-signature-account

            var devSender = DevWalletAccount.FromExpressWalletAccount(sender);
            var devReceiver = DevWalletAccount.FromExpressWalletAccount(receiver);

            var script = assetHash.MakeScript("transfer", devSender.ScriptHash, devReceiver.ScriptHash, amount);
            var cosigners = new[] { new Cosigner { Scopes = WitnessScope.CalledByEntry, Account = devSender.ScriptHash } };

            var tm = new TransactionManager(rpcClient, devSender.ScriptHash)
                .MakeTransaction(script, null, cosigners);

            AddSignatures(chain, tm, devSender);

            var tx = tm.Sign().Tx;

            return rpcClient.SendRawTransaction(tx);

            BigInteger GetAmount()
            {
                var nep5client = new Nep5API(rpcClient);
                if ("all".Equals(quantity, StringComparison.InvariantCultureIgnoreCase))
                {
                    return nep5client.BalanceOf(assetHash, sender.ScriptHash.ToScriptHash());
                }

                if (decimal.TryParse(quantity, out var value))
                {
                    var decimals = nep5client.Decimals(assetHash);
                    return Neo.Network.RPC.Utility.ToBigInteger(value, decimals);
                }

                throw new Exception("invalid quantity");
            }
        }

        public async Task<UInt256> DeployContract(ExpressChain chain, string contract, ExpressWalletAccount account)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

            var script = await CreateDeployScript();
            var tm = new TransactionManager(rpcClient, devAccount.ScriptHash)
                .MakeTransaction(script);

            AddSignatures(chain, tm, devAccount);
            var tx = tm.Sign().Tx;
            return rpcClient.SendRawTransaction(tx);
            
            async Task<byte[]> CreateDeployScript()
            {
                var scriptTask = File.ReadAllBytesAsync(contract);
                var manifestTask = File.ReadAllTextAsync(Path.ChangeExtension(contract, ".manifest.json"))
                    .ContinueWith(t => 
                    {
                        var json = Neo.IO.Json.JObject.Parse(t.Result);
                        return ContractManifest.FromJson(json);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                
                await Task.WhenAll(scriptTask, manifestTask).ConfigureAwait(false);

                using var sb = new ScriptBuilder();
                sb.EmitSysCall(InteropService.Contract.Create, scriptTask.Result, manifestTask.Result.ToString());
                return sb.ToArray();
            }
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

        public async Task<BigInteger> ShowBalance(ExpressChain chain, ExpressWalletAccount account, string asset)
        {
            var uri = chain.GetUri();
            var nep5client = new Nep5API(new RpcClient(uri.ToString()));

            var assetHash = NodeUtility.GetAssetId(asset);

            await Task.CompletedTask;
            return nep5client.BalanceOf(assetHash, account.ScriptHash.ToScriptHash());
        }

    }
}
