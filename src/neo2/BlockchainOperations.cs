using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using NeoExpress.Neo2.Models;
using NeoExpress.Neo2.Node;
using NeoExpress.Neo2.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Neo2
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

            if (preloadGas > 0)
            {
                var node = chain.ConsensusNodes[0];
                var folder = node.GetBlockchainPath();

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                PreloadGas(folder, chain, 0, preloadGas, writer, token);
            }

            return chain;

        }

        static ExpressChain CreateBlockchain(int count)
        {
            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            ushort GetPortNumber(int index, ushort portNumber) => (ushort)((49000 + (index * 1000)) + portNumber);

            try
            {
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
            finally
            {
                foreach (var (wallet, _) in wallets)
                {
                    wallet.Dispose();
                }
            }
        }

        static void PreloadGas(string directory, ExpressChain chain, int index, uint preloadGasAmount, TextWriter writer, CancellationToken cancellationToken)
        {
            if (!chain.InitializeProtocolSettings())
            {
                throw new Exception("could not initialize protocol settings");
            }
            var node = chain.ConsensusNodes[index];
            using var store = new RocksDbStore(directory);
            NodeUtility.Preload(preloadGasAmount, store, node, writer, cancellationToken);
        }

        public void ExportBlockchain(ExpressChain chain, string folder, string password, TextWriter writer)
        {
            void WriteNodeConfigJson(ExpressConsensusNode _node, string walletPath)
            {
                using (var stream = File.Open(Path.Combine(folder, $"{_node.Wallet.Name}.config.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("ApplicationConfiguration");
                    writer.WriteStartObject();

                    writer.WritePropertyName("Paths");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Chain");
                    writer.WriteValue("Chain_{0}");
                    writer.WritePropertyName("Index");
                    writer.WriteValue("Index_{0}");
                    writer.WriteEndObject();

                    writer.WritePropertyName("P2P");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Port");
                    writer.WriteValue(_node.TcpPort);
                    writer.WritePropertyName("WsPort");
                    writer.WriteValue(_node.WebSocketPort);
                    writer.WriteEndObject();

                    writer.WritePropertyName("RPC");
                    writer.WriteStartObject();
                    writer.WritePropertyName("BindAddress");
                    writer.WriteValue("127.0.0.1");
                    writer.WritePropertyName("Port");
                    writer.WriteValue(_node.RpcPort);
                    writer.WritePropertyName("SslCert");
                    writer.WriteValue("");
                    writer.WritePropertyName("SslCertPassword");
                    writer.WriteValue("");
                    writer.WriteEndObject();

                    writer.WritePropertyName("UnlockWallet");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Path");
                    writer.WriteValue(walletPath);
                    writer.WritePropertyName("Password");
                    writer.WriteValue(password);
                    writer.WritePropertyName("StartConsensus");
                    writer.WriteValue(true);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(true);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            void WriteProtocolJson()
            {
                using (var stream = File.Open(Path.Combine(folder, "protocol.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("ProtocolConfiguration");
                    writer.WriteStartObject();

                    writer.WritePropertyName("Magic");
                    writer.WriteValue(chain.Magic);
                    writer.WritePropertyName("AddressVersion");
                    writer.WriteValue(23);
                    writer.WritePropertyName("SecondsPerBlock");
                    writer.WriteValue(15);

                    writer.WritePropertyName("StandbyValidators");
                    writer.WriteStartArray();
                    for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                    {
                        var account = DevWalletAccount.FromExpressWalletAccount(chain.ConsensusNodes[i].Wallet.DefaultAccount);
                        var key = account.GetKey();
                        if (key != null)
                        {
                            writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                        }
                    }
                    writer.WriteEndArray();

                    writer.WritePropertyName("SeedList");
                    writer.WriteStartArray();
                    foreach (var node in chain.ConsensusNodes)
                    {
                        writer.WriteValue($"{IPAddress.Loopback}:{node.TcpPort}");
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Conensus Node wallet");

                var walletPath = Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                if (File.Exists(walletPath))
                {
                    File.Delete(walletPath);
                }

                ExportWallet(node.Wallet, walletPath, password);
                WriteNodeConfigJson(node, walletPath);
            }

            WriteProtocolJson();
        }

        public ExpressWallet CreateWallet(string name)
        {
            using (var wallet = new DevWallet(name))
            {
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                return wallet.ToExpressWallet();
            }
        }

        public void ExportWallet(ExpressWallet wallet, string filename, string password)
        {
            var devWallet = DevWallet.FromExpressWallet(wallet);
            devWallet.Export(filename, password);
        }

        public (byte[] signature, byte[] publicKey) Sign(ExpressWalletAccount account, byte[] data)
        {
            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

            var key = devAccount.GetKey();
            if (key == null)
                throw new InvalidOperationException();

            var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
            var signature = Neo.Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);
            return (signature, key.PublicKey.EncodePoint(true));
        }

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";
        private const string CHECKPOINT_EXTENSION = ".neo-express-checkpoint";

        private static string GetAddressFilePath(string directory) =>
            Path.Combine(directory, ADDRESS_FILENAME);

        public string ResolveCheckpointFileName(string? checkPointFileName)
        {
            checkPointFileName = string.IsNullOrEmpty(checkPointFileName)
                ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}{CHECKPOINT_EXTENSION}"
                : checkPointFileName;

            if (!Path.GetExtension(checkPointFileName).Equals(CHECKPOINT_EXTENSION))
            {
                checkPointFileName = checkPointFileName + CHECKPOINT_EXTENSION;
            }

            return Path.GetFullPath(checkPointFileName);
        }

        public async Task CreateCheckpoint(ExpressChain chain, string checkPointFileName, bool online, TextWriter writer)
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

            if (!online)
            {
                // Check to see if there's a neo-express blockchain currently running
                // by attempting to open a mutex with the multisig account address for 
                // a name. If so, do an online checkpoint instead of offline.

                var wallet = DevWallet.FromExpressWallet(node.Wallet);
                var account = wallet.GetAccounts().Single(a => a.Contract.Script.IsMultiSigContract());

                if (Mutex.TryOpenExisting(account.Address, out var _))
                {
                    online = true;
                }
            }

            if (online)
            {
                var uri = chain.GetUri();
                await NeoRpcClient.ExpressCreateCheckpoint(uri, checkPointFileName)
                    .ConfigureAwait(false);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint online");
            }
            else 
            {
                using var db = new RocksDbStore(folder);
                CreateCheckpoint(db, checkPointFileName, chain.Magic, chain.ConsensusNodes[0].Wallet.DefaultAccount.ScriptHash);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint offline");
            }
        }

        public void CreateCheckpoint(RocksDbStore db, string checkPointFileName, long magic, string scriptHash)
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));

            try
            {
                db.CheckPoint(tempPath);

                using (var stream = File.OpenWrite(GetAddressFilePath(tempPath)))
                using (var writer = new StreamWriter(stream))
                {
                    writer.WriteLine(magic);
                    writer.WriteLine(scriptHash);
                }

                if (File.Exists(checkPointFileName))
                {
                    throw new InvalidOperationException(checkPointFileName + " checkpoint file already exists");
                }
                System.IO.Compression.ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
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
                var blockchainDataPath = node.GetBlockchainPath();

                if (!force && Directory.Exists(blockchainDataPath))
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                ZipFile.ExtractToDirectory(checkPointArchive, checkpointTempPath);
                ValidateCheckpoint(checkpointTempPath, chain.Magic, node.Wallet.DefaultAccount);

                var addressFile = GetAddressFilePath(checkpointTempPath);
                if (File.Exists(addressFile))
                {
                    File.Delete(addressFile);
                }

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

        private static void ValidateCheckpoint(string checkPointDirectory, long magic, ExpressWalletAccount account)
        {
            var addressFile = GetAddressFilePath(checkPointDirectory);
            if (!File.Exists(addressFile))
            {
                throw new Exception("Invalid Checkpoint");
            }

            long checkPointMagic;
            string scriptHash;
            using (var stream = File.OpenRead(addressFile))
            using (var reader = new StreamReader(stream))
            {
                checkPointMagic = long.Parse(reader.ReadLine() ?? string.Empty);
                scriptHash = reader.ReadLine() ?? string.Empty;
            }

            if (magic != checkPointMagic || scriptHash != account.ScriptHash)
            {
                throw new Exception("Invalid Checkpoint");
            }
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

            if (!chain.InitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

#pragma warning disable IDE0067 // NodeUtility.RunAsync disposes the store when it's done
            return NodeUtility.RunAsync(new RocksDbStore(folder), node, writer, cancellationToken);
#pragma warning restore IDE0067 // Dispose objects before losing scope
        }

        public Task RunCheckpointAsync(ExpressChain chain, string checkPointArchive, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
        {
            string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                if (!chain.InitializeProtocolSettings(secondsPerBlock))
                {
                    throw new Exception("could not initialize protocol settings");
                }

                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                var node = chain.ConsensusNodes[0];
                ZipFile.ExtractToDirectory(checkPointArchive, checkpointTempPath);
                ValidateCheckpoint(checkpointTempPath, chain.Magic, node.Wallet.DefaultAccount);

    #pragma warning disable IDE0067 // NodeUtility.RunAsync disposes the store when it's done
                return NodeUtility.RunAsync(new CheckpointStore(checkpointTempPath), node, writer, cancellationToken);
    #pragma warning restore IDE0067 // Dispose objects before losing scope
            }
            finally
            {
                if (Directory.Exists(checkpointTempPath))
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            }
        }

        public async Task<ContractTransaction> Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            var uri = chain.GetUri();

            var unspents = (await NeoRpcClient.GetUnspents(uri, sender.ScriptHash)
                .ConfigureAwait(false))?.ToObject<UnspentsResponse>();
            if (unspents == null)
            {
                throw new Exception($"could not retrieve unspents for {nameof(sender)}");
            }

            var assetId = NodeUtility.GetAssetId(asset);
            var tx = RpcTransactionManager.CreateContractTransaction(
                    assetId, quantity, unspents, sender, receiver);

            tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, sender) };
            var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
            if (sendResult == null || !sendResult.Value<bool>())
            {
                throw new Exception("SendRawTransaction failed");
            }

            return tx;
        }

        public async Task<ClaimTransaction> Claim(ExpressChain chain, ExpressWalletAccount account)
        {
            var uri = chain.GetUri();
            var claimable = (await NeoRpcClient.GetClaimable(uri, account.ScriptHash)
                .ConfigureAwait(false))?.ToObject<ClaimableResponse>();
            if (claimable == null)
            {
                throw new Exception($"could not retrieve claimable for {nameof(account)}");
            }

            var gasHash = Neo.Ledger.Blockchain.UtilityToken.Hash;
            var tx = RpcTransactionManager.CreateClaimTransaction(account, claimable, gasHash);
            tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };
            var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
            if (sendResult == null || !sendResult.Value<bool>())
            {
                throw new Exception("SendRawTransaction failed");
            }

            return tx;
        }

        public async Task ShowTransaction(ExpressChain chain, string transactionId, TextWriter writer)
        {
            var uri = chain.GetUri();

            var rawTxResponseTask = NeoRpcClient.GetRawTransaction(uri, transactionId);
            var appLogResponseTask = NeoRpcClient.GetApplicationLog(uri, transactionId);
            await Task.WhenAll(rawTxResponseTask, appLogResponseTask);

            writer.WriteResult(rawTxResponseTask.Result);
            var appLogResponse = appLogResponseTask.Result ?? JValue.CreateString(string.Empty);
            if (appLogResponse.Type != JTokenType.String
                || appLogResponse.Value<string>().Length != 0)
            {
                writer.WriteResult(appLogResponse);
            }
        }

        static async Task Show(ExpressChain chain, string accountName, TextWriter writer, Func<Uri, string, Task<JToken?>> func, bool showJson = true, Action<JToken>? writeResponse = null)
        {
            var account = chain.GetAccount(accountName);
            if (account == null)
            {
                throw new Exception($"{accountName} wallet not found.");
            }

            var uri = chain.GetUri();
            var response = await func(uri, account.ScriptHash).ConfigureAwait(false);
            if (response == null)
            {
                throw new ApplicationException("no response from RPC server");
            }

            if (showJson || writeResponse == null)
            {
                writer.WriteResult(response);
            }
            else
            {
                writeResponse(response);
            }
        }

        public Task ShowAccount(ExpressChain chain, string name, bool showJson, TextWriter writer)
        {
            void WriteResponse(JToken token)
            {
                var response = token.ToObject<AccountResponse>() 
                    ?? throw new ApplicationException($"Cannot convert response to {nameof(AccountResponse)}");
                writer.WriteLine($"Account information for {name}:");
                foreach (var balance in response.Balances)
                {
                    writer.WriteLine($"  Asset {balance.Asset}: {balance.Value}");
                }
            }

            return Show(chain, name, writer, NeoRpcClient.GetAccountState, showJson, WriteResponse);
        }

        public Task ShowClaimable(ExpressChain chain, string name, bool showJson, TextWriter writer)
        {
                void WriteResponse(JToken token)
                {
                    var response = token.ToObject<ClaimableResponse>()
                        ?? throw new ApplicationException($"Cannot convert response to {nameof(ClaimableResponse)}");
                    writer.WriteLine($"Claimable GAS for {name}: {response.Unclaimed}");
                    foreach (var tx in response.Transactions)
                    {
                        writer.WriteLine($"  transaction {tx.TransactionId}({tx.Index}): {tx.Unclaimed}");
                    }
                }
            return Show(chain, name, writer, NeoRpcClient.GetClaimable, showJson, WriteResponse);
        }

        public Task ShowCoins(ExpressChain chain, string name, bool showJson, TextWriter writer)
        {
            return Show(chain, name, writer, NeoRpcClient.ExpressShowCoins);
        }

        public Task ShowGas(ExpressChain chain, string name, bool showJson, TextWriter writer)
        {
            void WriteResponse(JToken token)
            {
                var response = token.ToObject<UnclaimedResponse>()
                    ?? throw new ApplicationException($"Cannot convert response to {nameof(UnclaimedResponse)}");
                writer.WriteLine($"Unclaimed GAS for {name}: {response.Unclaimed}");
                writer.WriteLine($"    Available GAS: {response.Available}");
                writer.WriteLine($"  Unavailable GAS: {response.Unavailable}");
            }

            return Show(chain, name, writer, NeoRpcClient.GetUnclaimed, showJson, WriteResponse);
        }

        public Task ShowUnspent(ExpressChain chain, string name, bool showJson, TextWriter writer)
        {
            void WriteResponse(JToken token)
            {
                var response = token.ToObject<UnspentsResponse>()
                    ?? throw new ApplicationException($"Cannot convert response to {nameof(UnspentsResponse)}");
                writer.WriteLine($"Unspent assets for {name}");
                foreach (var balance in response.Balance)
                {
                    writer.WriteLine($"  {balance.AssetSymbol}: {balance.Amount}");
                    writer.WriteLine($"    asset hash: {balance.AssetHash}");
                    writer.WriteLine("    transactions:");
                    foreach (var tx in balance.Transactions)
                    {
                        writer.WriteLine($"      {tx.TransactionId}({tx.Index}): {tx.Value}");
                    }
                }
            }

            return Show(chain, name, writer, NeoRpcClient.GetUnspents, showJson, WriteResponse);
        }
    }
}
