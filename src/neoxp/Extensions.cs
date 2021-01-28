using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using OneOf;
using All = OneOf.Types.All;
namespace NeoExpress
{
    static class Extensions
    {
        public static string ResolveFileName(this IFileSystem fileSystem, string fileName, string extension, Func<string> getDefaultFileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = getDefaultFileName();
            }

            if (!fileSystem.Path.IsPathFullyQualified(fileName))
            {
                fileName = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), fileName);
            }

            return extension.Equals(fileSystem.Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase)
                ? fileName : fileName + extension;
        }

        const string GENESIS = "genesis";

        public static bool IsReservedName(this ExpressChain chain, string name)
        {
            if (string.Equals(GENESIS, name, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var node in chain.ConsensusNodes)
            {
                if (string.Equals(node.Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static ExpressWalletAccount? GetAccount(this ExpressChain chain, string name)
        {
            var wallet = (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase));
            if (wallet != null)
            {
                return wallet.DefaultAccount;
            }

            var node = chain.ConsensusNodes
                .SingleOrDefault(n => name.Equals(n.Wallet.Name, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.IsMultiSigContract()))
                    .FirstOrDefault();
            }

            return null;
        }
        public static ExpressWallet? GetWallet(this ExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));

        public static string GetTempFolder(this IFileSystem fileSystem) 
        {
            string path;
            do
            {
                path = fileSystem.Path.Combine(
                    fileSystem.Path.GetTempPath(), 
                    fileSystem.Path.GetRandomFileName());
            }
            while (fileSystem.Directory.Exists(path));
            return path;
        }

        public static string GetNodePath(this IFileSystem fileSystem, ExpressConsensusNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.Wallet == null) throw new ArgumentNullException(nameof(node.Wallet));
            var account = node.Wallet.Accounts.Single(a => a.IsDefault);

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express",
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        public static async Task<(NefFile nefFile, ContractManifest manifest)> LoadContractAsync(this IFileSystem fileSystem, string contractPath)
        {
            var nefTask = Task.Run(() =>
            {
                using var stream = fileSystem.File.OpenRead(contractPath);
                using var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, false);
                return reader.ReadSerializable<NefFile>();
            });

            var manifestTask = fileSystem.File.ReadAllBytesAsync(fileSystem.Path.ChangeExtension(contractPath, ".manifest.json"))
                .ContinueWith(t => ContractManifest.Parse(t.Result), default, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

            await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
            return (await nefTask, await manifestTask);
        }

        public static void InitalizeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            if (!chain.TryInitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }
        }

        public static bool TryInitializeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:MillisecondsPerBlock", $"{secondsPerBlock * 1000}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:ValidatorsCount", $"{chain.ConsensusNodes.Count}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new Neo.Wallets.KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyCommittee:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }

        public static Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, IExpressChainManager chainManager)
            => expressNode.GetContractParameterParserAsync(chainManager.Chain);

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, ExpressChain? chain = null)
        {
            ContractParameterParser.TryGetUInt160 tryGetAccount = (string name, out UInt160 scriptHash) =>
            {
                var account = chain?.GetAccount(name);
                if (account != null)
                {
                    scriptHash = account.AsUInt160();
                    return true;
                }

                scriptHash = null!;
                return false;
            };

            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            var lookup = contracts.ToDictionary(c => c.manifest.Name, c => c.hash);
            ContractParameterParser.TryGetUInt160 tryGetContract = (string name, out UInt160 scriptHash) =>
            {
                if (lookup.TryGetValue(name, out var value))
                {
                    scriptHash = value;
                    return true;
                }

                foreach (var kvp in lookup)
                {
                    if (string.Equals(name, kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        scriptHash = kvp.Value;
                        return true;
                    }
                }

                scriptHash = default!;
                return false;
            };

            return new ContractParameterParser(tryGetAccount, tryGetContract);
        }

        public static UInt160 ParseScriptHash(this ContractParameterParser parser, string hashOrContract)
        {
            if (UInt160.TryParse(hashOrContract, out var hash))
            {
                return hash;
            }

            if (parser.TryLoadScriptHash(hashOrContract, out var value))
            {
                return value;
            }

            throw new ArgumentException(nameof(hashOrContract));
        }

        public static async Task<UInt160> ParseAssetAsync(this IExpressNode expressNode, string asset)
        {
            if ("neo".Equals(asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.NEO.Hash;
            }

            if ("gas".Equals(asset, StringComparison.OrdinalIgnoreCase))
            {
                return NativeContract.GAS.Hash;
            }

            if (UInt160.TryParse(asset, out var uint160))
            {
                return uint160;
            }

            var contracts = await expressNode.ListNep17ContractsAsync().ConfigureAwait(false);
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].Symbol.Equals(asset, StringComparison.OrdinalIgnoreCase))
                {
                    return contracts[i].ScriptHash;
                }
            }

            throw new ArgumentException($"Unknown Asset \"{asset}\"", nameof(asset));
        }

        public static async Task<UInt256> TransferAsync(this IExpressNode expressNode, UInt160 asset, OneOf<decimal, All> quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            var senderHash = sender.AsUInt160();
            var receiverHash = receiver.AsUInt160();

            return await quantity.Match<Task<UInt256>>(TransferAmountAsync, TransferAllAsync);

            async Task<UInt256> TransferAmountAsync(decimal amount)
            {
                var results = await expressNode.InvokeAsync(asset.MakeScript("decimals")).ConfigureAwait(false);
                if (results.Stack.Length > 0 && results.Stack[0].Type == Neo.VM.Types.StackItemType.Integer)
                {
                    var decimals = (byte)(results.Stack[0].GetInteger());
                    var value = quantity.AsT0.ToBigInteger(decimals);
                    return await expressNode.ExecuteAsync(sender, asset.MakeScript("transfer", senderHash, receiverHash, value, null)).ConfigureAwait(false);
                }
                else
                {
                    throw new Exception("Invalid response from decimals operation");
                }
            }

            async Task<UInt256> TransferAllAsync(All _)
            {
                using var sb = new ScriptBuilder();
                // balanceOf operation places current balance on eval stack
                sb.EmitAppCall(asset, "balanceOf", senderHash);
                // transfer operation takes 4 arguments, amount is 3rd parameter
                // push null onto the stack and then switch positions of the top
                // two items on eval stack so null is 4th arg and balance is 3rd
                sb.Emit(OpCode.PUSHNULL);
                sb.Emit(OpCode.SWAP);
                sb.EmitPush(receiverHash);
                sb.EmitPush(senderHash);
                sb.EmitPush(4);
                sb.Emit(OpCode.PACK);
                sb.EmitPush("transfer");
                sb.EmitPush(asset);
                sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
                return await expressNode.ExecuteAsync(sender, sb.ToArray()).ConfigureAwait(false);
            }
        }

        public static async Task<UInt256> DeployAsync(this IExpressNode expressNode, NefFile nefFile, ContractManifest manifest, ExpressWalletAccount account)
        {
            // check for bad opcodes (logic borrowed from neo-cli LoadDeploymentScript)
            Neo.VM.Script script = nefFile.Script;
            for (var i = 0; i < script.Length;)
            {
                var instruction = script.GetInstruction(i);
                if (instruction == null)
                {
                    throw new FormatException($"null opcode found at {i}");
                }
                else
                {
                    if (!Enum.IsDefined(typeof(Neo.VM.OpCode), instruction.OpCode))
                    {
                        throw new FormatException($"Invalid opcode found at {i}-{((byte)instruction.OpCode).ToString("x2")}");
                    }

                    i += instruction.Size;
                }
            }

            using var sb = new ScriptBuilder();
            sb.EmitAppCall(NativeContract.Management.Hash, "deploy", nefFile.ToArray(), manifest.ToJson().ToString());
            return await expressNode.ExecuteAsync(account, sb.ToArray()).ConfigureAwait(false);
        }

        public static async Task<(RpcNep17Balance balance, Nep17Contract token)> GetBalanceAsync(this IExpressNode expressNode, ExpressWalletAccount account, string asset)
        {
            var accountHash = account.AsUInt160();
            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);

            using var sb = new ScriptBuilder();
            sb.EmitAppCall(assetHash, "balanceOf", accountHash);
            sb.EmitAppCall(assetHash, "symbol");
            sb.EmitAppCall(assetHash, "decimals");

            var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
            var stack = result.Stack;
            if (stack.Length >= 3)
            {
                var balance = stack[0].GetInteger();
                // TODO: retrieve name correctly
                var name = string.Empty; //Encoding.UTF8.GetString(stack[1].GetSpan());
                var symbol = Encoding.UTF8.GetString(stack[1].GetSpan());
                var decimals = (byte)(stack[2].GetInteger());

                return (
                    new RpcNep17Balance() { Amount = balance, AssetHash = assetHash }, 
                    new Nep17Contract(name, symbol, decimals, assetHash));
            }

            throw new Exception("invalid script results");
        }

        public static async Task<Block> GetBlockAsync(this IExpressNode expressNode, string blockHash)
        {
            if (string.IsNullOrEmpty(blockHash))
            {
                return await expressNode.GetLatestBlockAsync().ConfigureAwait(false);
            }

            if (UInt256.TryParse(blockHash, out var uint256))
            {
                return await expressNode.GetBlockAsync(uint256).ConfigureAwait(false);
            }

            if (uint.TryParse(blockHash, out var index))
            {
                return await expressNode.GetBlockAsync(index).ConfigureAwait(false);
            }

            throw new ArgumentException($"{nameof(blockHash)} must be block index, block hash or empty", nameof(blockHash));
        }

        public static BigDecimal ToBigDecimal(this RpcNep17Balance balance, byte decimals)
            => new BigDecimal(balance.Amount, decimals);
    }
}
