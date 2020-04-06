using Microsoft.Extensions.Configuration;
using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Abstractions;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using NeoExpress.Neo3.Node;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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

//         public void ExportBlockchain(ExpressChain chain, string folder, string password, TextWriter writer)
//         {
//             void WriteNodeConfigJson(ExpressConsensusNode _node, string walletPath)
//             {
//                 using (var stream = File.Open(Path.Combine(folder, $"{_node.Wallet.Name}.config.json"), FileMode.Create, FileAccess.Write))
//                 using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
//                 {
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("ApplicationConfiguration");
//                     writer.WriteStartObject();

//                     writer.WritePropertyName("Paths");
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("Chain");
//                     writer.WriteValue("Chain_{0}");
//                     writer.WritePropertyName("Index");
//                     writer.WriteValue("Index_{0}");
//                     writer.WriteEndObject();

//                     writer.WritePropertyName("P2P");
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("Port");
//                     writer.WriteValue(_node.TcpPort);
//                     writer.WritePropertyName("WsPort");
//                     writer.WriteValue(_node.WebSocketPort);
//                     writer.WriteEndObject();

//                     writer.WritePropertyName("RPC");
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("BindAddress");
//                     writer.WriteValue("127.0.0.1");
//                     writer.WritePropertyName("Port");
//                     writer.WriteValue(_node.RpcPort);
//                     writer.WritePropertyName("SslCert");
//                     writer.WriteValue("");
//                     writer.WritePropertyName("SslCertPassword");
//                     writer.WriteValue("");
//                     writer.WriteEndObject();

//                     writer.WritePropertyName("UnlockWallet");
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("Path");
//                     writer.WriteValue(walletPath);
//                     writer.WritePropertyName("Password");
//                     writer.WriteValue(password);
//                     writer.WritePropertyName("StartConsensus");
//                     writer.WriteValue(true);
//                     writer.WritePropertyName("IsActive");
//                     writer.WriteValue(true);
//                     writer.WriteEndObject();

//                     writer.WriteEndObject();
//                     writer.WriteEndObject();
//                 }
//             }

//             void WriteProtocolJson()
//             {
//                 using (var stream = File.Open(Path.Combine(folder, "protocol.json"), FileMode.Create, FileAccess.Write))
//                 using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
//                 {
//                     writer.WriteStartObject();
//                     writer.WritePropertyName("ProtocolConfiguration");
//                     writer.WriteStartObject();

//                     writer.WritePropertyName("Magic");
//                     writer.WriteValue(chain.Magic);
//                     writer.WritePropertyName("AddressVersion");
//                     writer.WriteValue(23);
//                     writer.WritePropertyName("SecondsPerBlock");
//                     writer.WriteValue(15);

//                     writer.WritePropertyName("StandbyValidators");
//                     writer.WriteStartArray();
//                     for (int i = 0; i < chain.ConsensusNodes.Count; i++)
//                     {
//                         var account = DevWalletAccount.FromExpressWalletAccount(chain.ConsensusNodes[i].Wallet.DefaultAccount);
//                         var key = account.GetKey();
//                         if (key != null)
//                         {
//                             writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
//                         }
//                     }
//                     writer.WriteEndArray();

//                     writer.WritePropertyName("SeedList");
//                     writer.WriteStartArray();
//                     foreach (var node in chain.ConsensusNodes)
//                     {
//                         writer.WriteValue($"{IPAddress.Loopback}:{node.TcpPort}");
//                     }
//                     writer.WriteEndArray();

//                     writer.WriteEndObject();
//                     writer.WriteEndObject();
//                 }
//             }

//             for (var i = 0; i < chain.ConsensusNodes.Count; i++)
//             {
//                 var node = chain.ConsensusNodes[i];
//                 writer.WriteLine($"Exporting {node.Wallet.Name} Conensus Node wallet");

//                 var walletPath = Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
//                 if (File.Exists(walletPath))
//                 {
//                     File.Delete(walletPath);
//                 }

//                 ExportWallet(node.Wallet, walletPath, password);
//                 WriteNodeConfigJson(node, walletPath);
//             }

//             WriteProtocolJson();
//         }

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

//         public void ExportWallet(ExpressWallet wallet, string filename, string password)
//         {
//             var devWallet = DevWallet.FromExpressWallet(wallet);
//             devWallet.Export(filename, password);
//         }

//         public (byte[] signature, byte[] publicKey) Sign(ExpressWalletAccount account, byte[] data)
//         {
//             var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

//             var key = devAccount.GetKey();
//             if (key == null)
//                 throw new InvalidOperationException();

//             var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
//             var signature = Neo.Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);
//             return (signature, key.PublicKey.EncodePoint(true));
//         }

//         private const string ADDRESS_FILENAME = "ADDRESS.neo-express";
//         private const string CHECKPOINT_EXTENSION = ".neo-express-checkpoint";

//         private static string GetAddressFilePath(string directory) =>
//             Path.Combine(directory, ADDRESS_FILENAME);

//         public string ResolveCheckpointFileName(string? checkPointFileName)
//         {
//             checkPointFileName = string.IsNullOrEmpty(checkPointFileName)
//                 ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}{CHECKPOINT_EXTENSION}"
//                 : checkPointFileName;

//             if (!Path.GetExtension(checkPointFileName).Equals(CHECKPOINT_EXTENSION))
//             {
//                 checkPointFileName = checkPointFileName + CHECKPOINT_EXTENSION;
//             }

//             return Path.GetFullPath(checkPointFileName);
//         }

//         public async Task CreateCheckpoint(ExpressChain chain, string checkPointFileName, TextWriter writer)
//         {
//             static bool NodeRunning(ExpressConsensusNode node)
//             {
//                 // Check to see if there's a neo-express blockchain currently running
//                 // by attempting to open a mutex with the multisig account address for 
//                 // a name. If so, do an online checkpoint instead of offline.

//                 var wallet = DevWallet.FromExpressWallet(node.Wallet);
//                 var account = wallet.GetAccounts().Single(a => a.IsMultiSigContract());

//                 if (Mutex.TryOpenExisting(account.Address, out var _))
//                 {
//                     return true;
//                 }

//                 return false;
//             }

//             if (File.Exists(checkPointFileName))
//             {
//                 throw new ArgumentException("Checkpoint file already exists", nameof(checkPointFileName));
//             }

//             if (chain.ConsensusNodes.Count != 1)
//             {
//                 throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
//             }

//             var node = chain.ConsensusNodes[0];
//             var folder = node.GetBlockchainPath();

//             if (NodeRunning(node))
//             {
//                 var uri = chain.GetUri();
//                 await NeoRpcClient.ExpressCreateCheckpoint(uri, checkPointFileName)
//                     .ConfigureAwait(false);
//                 writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint online");
//             }
//             else 
//             {
//                 using var db = new RocksDbStore(folder);
//                 CreateCheckpoint(db, checkPointFileName, chain.Magic, chain.ConsensusNodes[0].Wallet.DefaultAccount.ScriptHash);
//                 writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint offline");
//             }
//         }

//         internal void CreateCheckpoint(RocksDbStore db, string checkPointFileName, long magic, string scriptHash)
//         {
//             string tempPath;
//             do
//             {
//                 tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
//             }
//             while (Directory.Exists(tempPath));

//             try
//             {
//                 db.CheckPoint(tempPath);

//                 using (var stream = File.OpenWrite(GetAddressFilePath(tempPath)))
//                 using (var writer = new StreamWriter(stream))
//                 {
//                     writer.WriteLine(magic);
//                     writer.WriteLine(scriptHash);
//                 }

//                 if (File.Exists(checkPointFileName))
//                 {
//                     throw new InvalidOperationException(checkPointFileName + " checkpoint file already exists");
//                 }
//                 System.IO.Compression.ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
//             }
//             finally
//             {
//                 Directory.Delete(tempPath, true);
//             }
//         }

//         public void RestoreCheckpoint(ExpressChain chain, string checkPointArchive, bool force)
//         {
//             string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

//             try
//             {
//                 if (chain.ConsensusNodes.Count != 1)
//                 {
//                     throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
//                 }

//                 var node = chain.ConsensusNodes[0];
//                 var blockchainDataPath = node.GetBlockchainPath();

//                 if (!force && Directory.Exists(blockchainDataPath))
//                 {
//                     throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
//                 }

//                 ZipFile.ExtractToDirectory(checkPointArchive, checkpointTempPath);
//                 ValidateCheckpoint(checkpointTempPath, chain.Magic, node.Wallet.DefaultAccount);

//                 var addressFile = GetAddressFilePath(checkpointTempPath);
//                 if (File.Exists(addressFile))
//                 {
//                     File.Delete(addressFile);
//                 }

//                 if (Directory.Exists(blockchainDataPath))
//                 {
//                     Directory.Delete(blockchainDataPath, true);
//                 }

//                 Directory.Move(checkpointTempPath, blockchainDataPath);
//             }
//             finally
//             {
//                 if (Directory.Exists(checkpointTempPath))
//                 {
//                     Directory.Delete(checkpointTempPath, true);
//                 }
//             }
//         }

//         private static void ValidateCheckpoint(string checkPointDirectory, long magic, ExpressWalletAccount account)
//         {
//             var addressFile = GetAddressFilePath(checkPointDirectory);
//             if (!File.Exists(addressFile))
//             {
//                 throw new Exception("Invalid Checkpoint");
//             }

//             long checkPointMagic;
//             string scriptHash;
//             using (var stream = File.OpenRead(addressFile))
//             using (var reader = new StreamReader(stream))
//             {
//                 checkPointMagic = long.Parse(reader.ReadLine() ?? string.Empty);
//                 scriptHash = reader.ReadLine() ?? string.Empty;
//             }

//             if (magic != checkPointMagic || scriptHash != account.ScriptHash)
//             {
//                 throw new Exception("Invalid Checkpoint");
//             }
//         }

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

//         public Task RunCheckpointAsync(ExpressChain chain, string checkPointArchive, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
//         {
//             string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
//             try
//             {
//                 if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
//                 {
//                     throw new Exception("could not initialize protocol settings");
//                 }

//                 if (chain.ConsensusNodes.Count != 1)
//                 {
//                     throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
//                 }

//                 var node = chain.ConsensusNodes[0];
//                 ZipFile.ExtractToDirectory(checkPointArchive, checkpointTempPath);
//                 ValidateCheckpoint(checkpointTempPath, chain.Magic, node.Wallet.DefaultAccount);

//     #pragma warning disable IDE0067 // NodeUtility.RunAsync disposes the store when it's done
//                 return NodeUtility.RunAsync(new CheckpointStore(checkpointTempPath), node, writer, cancellationToken);
//     #pragma warning restore IDE0067 // Dispose objects before losing scope
//             }
//             finally
//             {
//                 if (Directory.Exists(checkpointTempPath))
//                 {
//                     Directory.Delete(checkpointTempPath, true);
//                 }
//             }
//         }

        static async Task<Transaction> MakeTransaction(RpcClient rpcClient, byte[] script, UInt160 senderScriptHash, TransactionAttribute[]? attributes = null, Cosigner[]? cosigners = null)
        {
            attributes ??= Array.Empty<TransactionAttribute>();
            cosigners ??= Array.Empty<Cosigner>();

            uint height = (await rpcClient.GetBlockCountAsync()) - 1;
            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)ThreadStaticRandom.Next(),
                Script = script,
                Sender = senderScriptHash,
                ValidUntilBlock = height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = attributes,
                Cosigners = cosigners,
                Witnesses = Array.Empty<Witness>()
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(null);
            var result = await rpcClient.InvokeScriptAsync(script, hashes);
            tx.SystemFee = Math.Max(long.Parse(result.GasConsumed) - ApplicationEngine.GasFree, 0);
            if (tx.SystemFee > 0)
            {
                long d = (long)NativeContract.GAS.Factor;
                long remainder = tx.SystemFee % d;
                if (remainder > 0)
                    tx.SystemFee += d - remainder;
                else if (remainder < 0)
                    tx.SystemFee -= remainder;
            }

            return tx;
        }

        static async Task<long> CalculateNetworkFee(Transaction tx, RpcClient rpcClient, IReadOnlyList<WalletAccount> signers)
        {
            long networkFee = 0;
            UInt160[] hashes = tx.GetScriptHashesForVerifying(null);
            int size = Transaction.HeaderSize + tx.Attributes.GetVarSize() + tx.Cosigners.GetVarSize() + tx.Script.GetVarSize() + Neo.IO.Helper.GetVarSize(hashes.Length);
            foreach (UInt160 hash in hashes)
            {
                byte[]? witnessScript = signers.FirstOrDefault(a => a.Contract.ScriptHash == hash)?.Contract?.Script;
                if (witnessScript == null || witnessScript.Length == 0)
                {
                    try
                    {
                        witnessScript = (await rpcClient.GetContractStateAsync(hash.ToString()))?.Script;
                    }
                    catch {}

                    if (witnessScript is null) continue;
                    networkFee += Wallet.CalculateNetworkFee(witnessScript, ref size);
                }
            }
            networkFee += size * (await rpcClient.GetFeePerByteAsync());
            return networkFee;
        }

        static IEnumerable<ExpressWalletAccount> GetMultiSigAccounts(ExpressChain chain, string scriptHash)
        {
            return chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.FirstOrDefault(a => a.ScriptHash == scriptHash))
                .Where(a => a != null);
        }

        static async Task Sign(Transaction tx, RpcClient rpcClient, ExpressWalletAccount sender, IEnumerable<ExpressWalletAccount> signers)
        {
            var devSigners = signers.Select(DevWalletAccount.FromExpressWalletAccount).ToList();
            tx.NetworkFee = await CalculateNetworkFee(tx, rpcClient, devSigners).ConfigureAwait(false);

            var gasBalance = await rpcClient.BalanceOfAsync(NativeContract.GAS.Hash, sender.ScriptHash.ToScriptHash());
            if (gasBalance < tx.SystemFee + tx.NetworkFee)
            {
                throw new InvalidOperationException($"Insufficient GAS in address: {sender.ScriptHash}");
            }

            var context = new ContractParametersContext(tx);
            foreach (var signer in devSigners)
            {
                var key = signer.GetKey() ?? throw new Exception($"{signer.Address} missing key");
                var signature = tx.Sign(key);
                if (!context.AddSignature(signer.Contract, key.PublicKey, signature))
                {
                    throw new Exception("AddSignature Failed");
                }
            }

            if (!context.Completed)
            {
                throw new Exception($"Insufficient signatures");
            }

            tx.Witnesses = context.GetWitnesses();
        }

        public async Task<UInt256> Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var assetHash = NodeUtility.GetAssetId(asset);
            var amount = await GetAmount(rpcClient, assetHash).ConfigureAwait(false);

            // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/transaction.md#constructing-a-transaction-to-transfer-from-multi-signature-account
            var senderScriptHash = sender.ScriptHash.ToScriptHash();
            var script = assetHash.MakeScript("transfer", senderScriptHash, receiver.ScriptHash.ToScriptHash(), amount);
            var cosigners = new[] { new Cosigner { Scopes = WitnessScope.CalledByEntry, Account = senderScriptHash } };
            var tx = await MakeTransaction(rpcClient, script, senderScriptHash, null, cosigners).ConfigureAwait(false);
            
            var signers = sender.IsMultiSigContract()
                ? GetMultiSigAccounts(chain, sender.ScriptHash).Take(sender.Contract.Parameters.Count)
                : Enumerable.Repeat(sender, 1);

            await Sign(tx, rpcClient, sender, signers).ConfigureAwait(false);
            return await rpcClient.SendRawTransactionAsync(tx);

            async Task<BigInteger> GetAmount(RpcClient _rpcClient, UInt160 _assetHash)
            {
                if ("all".Equals(quantity, StringComparison.InvariantCultureIgnoreCase))
                {
                    return await rpcClient.BalanceOfAsync(assetHash, sender.ScriptHash.ToScriptHash());
                }

                if (decimal.TryParse(quantity, out var value))
                {
                    var decimals = await rpcClient.DecimalsAsync(assetHash).ConfigureAwait(false);
                    return Neo.Network.RPC.Utility.ToBigInteger(value, decimals);
                }

                throw new Exception("invalid quantity");
            }
        }

//         public async Task<ClaimTransaction> Claim(ExpressChain chain, ExpressWalletAccount account)
//         {
//             var uri = chain.GetUri();
//             var claimable = (await NeoRpcClient.GetClaimable(uri, account.ScriptHash)
//                 .ConfigureAwait(false))?.ToObject<ClaimableResponse>();
//             if (claimable == null)
//             {
//                 throw new Exception($"could not retrieve claimable for {nameof(account)}");
//             }

//             var gasHash = Neo.Ledger.Blockchain.UtilityToken.Hash;
//             var tx = RpcTransactionManager.CreateClaimTransaction(account, claimable, gasHash);
//             tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };
//             var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
//             if (sendResult == null || !sendResult.Value<bool>())
//             {
//                 throw new Exception("SendRawTransaction failed");
//             }

//             return tx;
//         }

//         public async Task ShowTransaction(ExpressChain chain, string transactionId, TextWriter writer)
//         {
//             var uri = chain.GetUri();

//             var rawTxResponseTask = NeoRpcClient.GetRawTransaction(uri, transactionId);
//             var appLogResponseTask = NeoRpcClient.GetApplicationLog(uri, transactionId);
//             await Task.WhenAll(rawTxResponseTask, appLogResponseTask);

//             writer.WriteResult(rawTxResponseTask.Result);
//             var appLogResponse = appLogResponseTask.Result ?? JValue.CreateString(string.Empty);
//             if (appLogResponse.Type != JTokenType.String
//                 || appLogResponse.Value<string>().Length != 0)
//             {
//                 writer.WriteResult(appLogResponse);
//             }
//         }

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

//         async Task Show(ExpressChain chain, string accountName, TextWriter writer, Func<Uri, string, Task<JToken?>> func, bool showJson = true, Action<JToken>? writeResponse = null)
//         {
//             var account = GetAccount(chain, accountName);
//             if (account == null)
//             {
//                 throw new Exception($"{accountName} wallet not found.");
//             }

//             var uri = chain.GetUri();
//             var response = await func(uri, account.ScriptHash).ConfigureAwait(false);
//             if (response == null)
//             {
//                 throw new ApplicationException("no response from RPC server");
//             }

//             if (showJson || writeResponse == null)
//             {
//                 writer.WriteResult(response);
//             }
//             else
//             {
//                 writeResponse(response);
//             }
//         }

//         public Task ShowAccount(ExpressChain chain, string name, bool showJson, TextWriter writer)
//         {
//             void WriteResponse(JToken token)
//             {
//                 var response = token.ToObject<AccountResponse>() 
//                     ?? throw new ApplicationException($"Cannot convert response to {nameof(AccountResponse)}");
//                 writer.WriteLine($"Account information for {name}:");
//                 foreach (var balance in response.Balances)
//                 {
//                     writer.WriteLine($"  Asset {balance.Asset}: {balance.Value}");
//                 }
//             }

//             return Show(chain, name, writer, NeoRpcClient.GetAccountState, showJson, WriteResponse);
//         }

//         public Task ShowClaimable(ExpressChain chain, string name, bool showJson, TextWriter writer)
//         {
//                 void WriteResponse(JToken token)
//                 {
//                     var response = token.ToObject<ClaimableResponse>()
//                         ?? throw new ApplicationException($"Cannot convert response to {nameof(ClaimableResponse)}");
//                     writer.WriteLine($"Claimable GAS for {name}: {response.Unclaimed}");
//                     foreach (var tx in response.Transactions)
//                     {
//                         writer.WriteLine($"  transaction {tx.TransactionId}({tx.Index}): {tx.Unclaimed}");
//                     }
//                 }
//             return Show(chain, name, writer, NeoRpcClient.GetClaimable, showJson, WriteResponse);
//         }

//         public Task ShowCoins(ExpressChain chain, string name, bool showJson, TextWriter writer)
//         {
//             return Show(chain, name, writer, NeoRpcClient.ExpressShowCoins);
//         }

//         public Task ShowGas(ExpressChain chain, string name, bool showJson, TextWriter writer)
//         {
//             void WriteResponse(JToken token)
//             {
//                 var response = token.ToObject<UnclaimedResponse>()
//                     ?? throw new ApplicationException($"Cannot convert response to {nameof(UnclaimedResponse)}");
//                 writer.WriteLine($"Unclaimed GAS for {name}: {response.Unclaimed}");
//                 writer.WriteLine($"    Available GAS: {response.Available}");
//                 writer.WriteLine($"  Unavailable GAS: {response.Unavailable}");
//             }

//             return Show(chain, name, writer, NeoRpcClient.GetUnclaimed, showJson, WriteResponse);
//         }

//         public Task ShowUnspent(ExpressChain chain, string name, bool showJson, TextWriter writer)
//         {
//             void WriteResponse(JToken token)
//             {
//                 var response = token.ToObject<UnspentsResponse>()
//                     ?? throw new ApplicationException($"Cannot convert response to {nameof(UnspentsResponse)}");
//                 writer.WriteLine($"Unspent assets for {name}");
//                 foreach (var balance in response.Balance)
//                 {
//                     writer.WriteLine($"  {balance.AssetSymbol}: {balance.Amount}");
//                     writer.WriteLine($"    asset hash: {balance.AssetHash}");
//                     writer.WriteLine("    transactions:");
//                     foreach (var tx in balance.Transactions)
//                     {
//                         writer.WriteLine($"      {tx.TransactionId}({tx.Index}): {tx.Value}");
//                     }
//                 }
//             }

//             return Show(chain, name, writer, NeoRpcClient.GetUnspents, showJson, WriteResponse);
//         }

//         static ExpressContract LoadContract(string avmFile)
//         {
//             static AbiContract LoadAbiContract(string avmFile)
//             {
//                 string abiFile = Path.ChangeExtension(avmFile, ".abi.json");
//                 if (!File.Exists(abiFile))
//                 {
//                     throw new ApplicationException($"there is no .abi.json file for {avmFile}.");
//                 }

//                 var serializer = new JsonSerializer();
//                 using var stream = File.OpenRead(abiFile);
//                 using var reader = new JsonTextReader(new StreamReader(stream));
//                 return serializer.Deserialize<AbiContract>(reader)
//                     ?? throw new ApplicationException($"Cannot load contract abi information from {abiFile}");
//             }

//             System.Diagnostics.Debug.Assert(File.Exists(avmFile));

//             var abiContract = LoadAbiContract(avmFile);
//             var name = Path.GetFileNameWithoutExtension(avmFile);
//             var contractData = File.ReadAllBytes(avmFile).ToHexString();
//             return Convert(abiContract, name, contractData);
//         }

//         static ExpressContract Convert(ContractState contractState)
//         {
//             var properties = new Dictionary<string, string>()
//             {
//                 { "has-dynamic-invoke", contractState.Properties.DynamicInvoke.ToString() },
//                 { "has-storage", contractState.Properties.Storage.ToString() }
//             };

//             var entrypoint = "Main"; 
//             var @params = contractState.Parameters.Select((type, index) => 
//                 new ExpressContract.Parameter()
//                 {
//                     Name = $"parameter{index}",
//                     Type = type
//                 }
//             );

//             var function = new ExpressContract.Function()
//             {
//                 Name = entrypoint,
//                 Parameters = @params.ToList(),
//                 ReturnType = contractState.ReturnType,
//             };

//             return new ExpressContract()
//             {
//                 Name = contractState.Name,
//                 Hash = contractState.Hash,
//                 EntryPoint = entrypoint,
//                 ContractData = contractState.Script,
//                 Functions = new List<ExpressContract.Function>() { function },                
//                 Properties = properties
//             };

//         }
//         static ExpressContract Convert(AbiContract abiContract, string? name = null, string? contractData = null)
//         {
//             static ExpressContract.Function ToExpressContractFunction(AbiContract.Function function)
//                 => new ExpressContract.Function
//                 {
//                     Name = function.Name,
//                     ReturnType = function.ReturnType,
//                     Parameters = function.Parameters.Select(p => new ExpressContract.Parameter
//                     {
//                         Name = p.Name,
//                         Type = p.Type
//                     }).ToList()
//                 };
            
//             var properties = abiContract.Metadata == null
//                 ? new Dictionary<string, string>()
//                 : new Dictionary<string, string>()
//                 {
//                     { "title", abiContract.Metadata.Title },
//                     { "description", abiContract.Metadata.Description },
//                     { "version", abiContract.Metadata.Version },
//                     { "email", abiContract.Metadata.Email },
//                     { "author", abiContract.Metadata.Author },
//                     { "has-storage", abiContract.Metadata.HasStorage.ToString() },
//                     { "has-dynamic-invoke", abiContract.Metadata.HasDynamicInvoke.ToString() },
//                     { "is-payable", abiContract.Metadata.IsPayable.ToString() }
//                 };

//             return new ExpressContract()
//             {
//                 Name = name ?? abiContract.Metadata?.Title ?? string.Empty,
//                 Hash = abiContract.Hash,
//                 EntryPoint = abiContract.Entrypoint,
//                 ContractData = contractData ?? string.Empty,
//                 Functions = abiContract.Functions.Select(ToExpressContractFunction).ToList(),
//                 Events = abiContract.Events.Select(ToExpressContractFunction).ToList(),
//                 Properties = properties
//             };
//         }

//         static AbiContract Convert(ExpressContract contract)
//         {
//             static AbiContract.Function ToAbiContractFunction(ExpressContract.Function function)
//                 => new AbiContract.Function
//                 {
//                     Name = function.Name,
//                     ReturnType = function.ReturnType,
//                     Parameters = function.Parameters.Select(p => new AbiContract.Parameter
//                     {
//                         Name = p.Name,
//                         Type = p.Type
//                     }).ToList()
//                 };

//             static AbiContract.ContractMetadata ToAbiContractMetadata(Dictionary<string, string> metadata)
//             {
//                 var contractMetadata = new AbiContract.ContractMetadata();
//                 if (metadata.TryGetValue("title", out var title))
//                 {
//                     contractMetadata.Title = title;
//                 }
//                 if (metadata.TryGetValue("description", out var description))
//                 {
//                     contractMetadata.Description = description;
//                 }
//                 if (metadata.TryGetValue("version", out var version))
//                 {
//                     contractMetadata.Version = version;
//                 }
//                 if (metadata.TryGetValue("email", out var email))
//                 {
//                     contractMetadata.Description = email;
//                 }
//                 if (metadata.TryGetValue("author", out var author))
//                 {
//                     contractMetadata.Author = author;
//                 }
//                 if (metadata.TryGetValue("has-storage", out var hasStorageString)
//                     && bool.TryParse(hasStorageString, out var hasStorage))
//                 {
//                     contractMetadata.HasStorage = hasStorage;
//                 }
//                 if (metadata.TryGetValue("has-dynamic-invoke", out var hasDynamicInvokeString)
//                     && bool.TryParse(hasDynamicInvokeString, out var hasDynamicInvoke))
//                 {
//                     contractMetadata.HasDynamicInvoke = hasDynamicInvoke;
//                 }
//                 if (metadata.TryGetValue("is-payable", out var isPayableString)
//                     && bool.TryParse(hasStorageString, out var isPayable))
//                 {
//                     contractMetadata.IsPayable = isPayable;
//                 }

//                 return contractMetadata;
//             }

//             return new AbiContract()
//             {
//                 Hash = contract.Hash,
//                 Entrypoint = contract.EntryPoint,
//                 Functions = contract.Functions.Select(ToAbiContractFunction).ToList(),
//                 Events = contract.Events.Select(ToAbiContractFunction).ToList(),
//                 Metadata = ToAbiContractMetadata(contract.Properties)
//             };
//         }

//         public bool TryLoadContract(string path, [MaybeNullWhen(false)] out ExpressContract contract, [MaybeNullWhen(true)] out string errorMessage)
//         {
//             if (Directory.Exists(path))
//             {
//                 var avmFiles = Directory.EnumerateFiles(path, "*.avm");
//                 var avmFileCount = avmFiles.Count();
//                 if (avmFileCount == 1)
//                 {
//                     contract = LoadContract(avmFiles.Single());
//                     errorMessage = null!;
//                     return true;
//                 }

//                 contract = null!;
//                 errorMessage = avmFileCount == 0
//                     ? $"There are no .avm files in {path}"
//                     : $"There is more than one .avm file in {path}. Please specify file name directly";
//                 return false;
//             }

//             if (File.Exists(path) && Path.GetExtension(path) == ".avm")
//             {
//                 contract = LoadContract(path);
//                 errorMessage = null!;
//                 return true;
//             }

//             contract = null!;
//             errorMessage = $"{path} is not an .avm file.";
//             return false;
//         }

//         public async Task<InvocationTransaction> DeployContract(ExpressChain chain, ExpressContract contract, ExpressWalletAccount account, bool saveMetadata = true)
//         {
//             var uri = chain.GetUri();

//             var contractState = await SwallowException(NeoRpcClient.GetContractState(uri, contract.Hash))
//                 .ConfigureAwait(false);

//             if (contractState != null)
//             {
//                 throw new Exception($"Contract {contract.Name} ({contract.Hash}) already deployed");
//             }

//             var unspents = (await NeoRpcClient.GetUnspents(uri, account.ScriptHash)
//                 .ConfigureAwait(false))?.ToObject<UnspentsResponse>();
//             if (unspents == null)
//             {
//                 throw new Exception($"could not retrieve unspents for account");
//             }

//             var tx = RpcTransactionManager.CreateDeploymentTransaction(contract, 
//                 account, unspents);
//             tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };

//             var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
//             if (sendResult == null || !sendResult.Value<bool>())
//             {
//                 throw new Exception("SendRawTransaction failed");
//             }

//             if (saveMetadata)
//             {
//                 var abiContract = Convert(contract);
//                 await NeoRpcClient.SaveContractMetadata(uri, abiContract.Hash, abiContract);
//             }

//             return tx;
//         }

//         static Task<T?> SwallowException<T>(Task<T?> task)
//             where T : class
//         {
//             return task.ContinueWith(t => {
//                 if (task.IsCompletedSuccessfully)
//                 {
//                     return task.Result;
//                 }
//                 else
//                 {
//                     return null;
//                 }
//             });
//         }

//         public async Task<ExpressContract?> GetContract(ExpressChain chain, string scriptHash)
//         {
//             var uri = chain.GetUri();

//             var getContractStateTask = SwallowException(NeoRpcClient.GetContractState(uri, scriptHash));
//             var getContractMetadataTask = SwallowException(NeoRpcClient.GetContractMetadata(uri, scriptHash));
//             await Task.WhenAll(getContractStateTask, getContractMetadataTask);

//             if (getContractStateTask.Result != null
//                 && getContractMetadataTask.Result != null)
//             {
//                 var contractData = getContractMetadataTask.Result.Value<string>("script");
//                 var name = getContractMetadataTask.Result.Value<string>("name");
//                 var abiContract = getContractMetadataTask.Result.ToObject<AbiContract>();

//                 if (abiContract != null)
//                 {
//                     return Convert(abiContract, name, contractData);
//                 }
//             }

//             if (getContractStateTask.Result != null)
//             {
//                 var contractState = getContractStateTask.Result.ToObject<ContractState>();
//                 if (contractState != null)
//                 {
//                     return Convert(contractState);
//                 }
//             }

//             throw new Exception($"Contract {scriptHash} not deployed");
//         }

//         public async Task<List<ExpressContract>> ListContracts(ExpressChain chain)
//         {
//             var uri = chain.GetUri();
//             var json = await NeoRpcClient.ListContracts(uri);

//             if (json != null && json is JArray jObject)
//             {
//                 var contracts = new List<ExpressContract>(jObject.Count);
//                 foreach (var obj in jObject)
//                 {
//                     var type = obj.Value<string>("type");
//                     if (type == "metadata")
//                     {
//                         var contract = obj.ToObject<AbiContract>();
//                         Debug.Assert(contract != null);
//                         contracts.Add(Convert(contract!));
//                     }
//                     else
//                     {
//                         Debug.Assert(type == "state");
//                         var contract = obj.ToObject<ContractState>();
//                         Debug.Assert(contract != null);
//                         contracts.Add(Convert(contract!));
//                     }
//                 }
                
//                 return contracts;
//             }

//             return new List<ExpressContract>(0);
//         }

//         public async Task<List<ExpressStorage>> GetStorage(ExpressChain chain, string scriptHash)
//         {
//             var uri = chain.GetUri();
//             var json = await NeoRpcClient.ExpressGetContractStorage(uri, scriptHash);
//             if (json != null && json is JArray array)
//             {
//                 var storages = new List<ExpressStorage>(array.Count);
//                 foreach (var s in array)
//                 {
//                     var storage = new ExpressStorage()
//                     {
//                         Key = s.Value<string>("key"),
//                         Value = s.Value<string>("value"),
//                         Constant = s.Value<bool>("constant")
//                     };
//                     storages.Add(storage);
//                 }
//                 return storages;
//             }

//             return new List<ExpressStorage>(0);
//         }
    }
}
