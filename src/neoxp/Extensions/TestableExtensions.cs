using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using NeoExpress.Node;
using Neo.Wallets.NEP6;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using Neo.Network.RPC.Models;
using Neo.VM;
using Neo.BlockchainToolkit;
using Neo.SmartContract.Manifest;
using Neo.Network.P2P.Payloads;

namespace NeoExpress
{
    static class TestableExtensions
    {
        public static bool IsRunning(this IExpressChain @this)
        {
            for (var i = 0; i < @this.ConsensusNodes.Count; i++)
            {
                if (@this.ConsensusNodes[i].IsRunning())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRunning(this ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return System.Threading.Mutex.TryOpenExisting(
                ExpressSystem.GLOBAL_PREFIX + account.ScriptHash,
                out var _);
        }

        public static ProtocolSettings GetProtocolSettings(this IExpressChain? chain, uint? secondsPerBlock = null)
        {
            var blockTime = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : (chain?.TryReadSetting("chain.SecondsPerBlock", uint.TryParse, out uint setting) ?? false)
                    ? setting
                    : 0;

            return chain == null
                ? ProtocolSettings.Default
                : ProtocolSettings.Default with
                {
                    Network = chain.Network,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = blockTime == 0 ? 15000 : blockTime * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

            static Neo.Cryptography.ECC.ECPoint GetPublicKey(ExpressConsensusNode node)
                => new Neo.Wallets.KeyPair(Convert.FromHexString(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single())).PublicKey;
        }

        public static Contract GetConsensusContract(this IExpressChain chain)
        {
            List<Neo.Cryptography.ECC.ECPoint> publicKeys = new(chain.ConsensusNodes.Count);
            foreach (var node in chain.ConsensusNodes)
            {
                var account = node.Wallet.DefaultAccount ?? throw new Exception("Missing Default Account");
                var privateKey = Convert.FromHexString(account.PrivateKey);
                var keyPair = new Neo.Wallets.KeyPair(privateKey);
                publicKeys.Add(keyPair.PublicKey);
            }

            var m = publicKeys.Count * 2 / 3 + 1;
            return Contract.CreateMultiSigContract(m, publicKeys);
        }

        public static KeyPair[] GetConsensusNodeKeys(this IExpressChain chain)
            => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray();

        public static ExpressWallet? GetWallet(this IExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<Wallet> GetMultiSigWallets(this IExpressChain chain, UInt160 accountHash)
        {
            var wallets = new List<Wallet>();
            var settings = chain.GetProtocolSettings();

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(chain.ConsensusNodes[i].Wallet, settings);
                if (wallet.GetAccount(accountHash) is not null) wallets.Add(wallet);
            }

            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(chain.Wallets[i], settings);
                if (wallet.GetAccount(accountHash) is not null) wallets.Add(wallet);
            }

            return wallets;
        }

        public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> @this) => @this.ToList();

        public static int Execute(this CommandLineApplication app, Delegate @delegate)
        {
            if (@delegate.Method.ReturnType != typeof(void)) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                @delegate.DynamicInvoke(@params);
                return 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                app.WriteException(ex.InnerException);
                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        public static async Task<int> ExecuteAsync(this CommandLineApplication app, Delegate @delegate, CancellationToken token = default)
        {
            if (@delegate.Method.ReturnType != typeof(Task)) throw new Exception();
            if (@delegate.Method.ReturnType.GenericTypeArguments.Length > 0) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                var @return = @delegate.DynamicInvoke(@params) ?? throw new Exception();
                await ((Task)@return).ConfigureAwait(false);
                return 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                app.WriteException(ex.InnerException);
                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        static object[] BindParameters(Delegate @delegate, IServiceProvider provider, CancellationToken token = default)
        {
            var paramInfos = @delegate.Method.GetParameters() ?? throw new Exception();
            var @params = new object[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                @params[i] = paramInfos[i].ParameterType == typeof(CancellationToken)
                    ? token : provider.GetRequiredService(paramInfos[i].ParameterType);
            }
            return @params;
        }

        public static Neo.IO.Json.JObject ExportNEP6(this ExpressWallet @this, string password, ProtocolSettings settings)
        {
            var nep6Wallet = new NEP6Wallet(string.Empty, password, settings, @this.Name);
            foreach (var account in @this.Accounts)
            {
                var key = Convert.FromHexString(account.PrivateKey);
                var nep6Account = nep6Wallet.CreateAccount(key);
                nep6Account.Label = account.Label;
                nep6Account.IsDefault = account.IsDefault;
            }
            return nep6Wallet.ToJson();
        }

        public static async Task<UInt256> SubmitTransactionAsync(this IExpressNode expressNode, Script script, string accountName, string password, WitnessScope witnessScope, decimal additionalGas = 0m)
        {
            var (wallet, accountHash) = expressNode.Chain.ResolveSigner(accountName, password);
            return await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, script, additionalGas).ConfigureAwait(false);
        }

        public static async Task<RpcInvokeResult> InvokeForResultsAsync(this IExpressNode expressNode, Script script, string accountName, WitnessScope witnessScope)
        {
            Signer? signer = expressNode.Chain.TryResolveSigner(accountName, string.Empty, out _, out var accountHash)
                ? signer = new Signer
                {
                    Account = accountHash,
                    Scopes = witnessScope,
                    AllowedContracts = Array.Empty<UInt160>(),
                    AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>(),
                    Rules = Array.Empty<WitnessRule>()
                }
                : null;

            return await expressNode.InvokeAsync(script, signer).ConfigureAwait(false);
        }

        public static async Task<Script> BuildInvocationScriptAsync(this IExpressNode expressNode, string contract, string operation, IReadOnlyList<string>? arguments = null)
        {
            if (string.IsNullOrEmpty(operation))
                throw new InvalidOperationException($"invalid contract operation \"{operation}\"");

            var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
            var scriptHash = parser.TryLoadScriptHash(contract, out var value)
                ? value
                : UInt160.TryParse(contract, out var uint160)
                    ? uint160
                    : throw new InvalidOperationException($"contract \"{contract}\" not found");

            arguments ??= Array.Empty<string>();
            var @params = new ContractParameter[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                @params[i] = ConvertArg(arguments[i], parser);
            }

            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitDynamicCall(scriptHash, operation, @params);
            return scriptBuilder.ToArray();

            static ContractParameter ConvertArg(string arg, ContractParameterParser parser)
            {
                if (bool.TryParse(arg, out var boolArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Boolean,
                        Value = boolArg
                    };
                }

                if (long.TryParse(arg, out var longArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Integer,
                        Value = new System.Numerics.BigInteger(longArg)
                    };
                }

                return parser.ParseParameter(arg);
            }
        }



        public static UInt160 ResolveAccountHash(this IExpressChain chain, string name)
            => chain.TryResolveAccountHash(name, out var hash)
                ? hash
                : throw new Exception("TryResolveAccountHash failed");

        public static bool TryResolveAccountHash(this IExpressChain chain, string name, out UInt160 accountHash)
        {
            if (chain.Wallets != null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        accountHash = chain.Wallets[i].DefaultAccount.GetScriptHash();
                        return true;
                    }
                }
            }

            Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var nodeWallet = chain.ConsensusNodes[i].Wallet;
                if (string.Equals(name, nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                {
                    accountHash = nodeWallet.DefaultAccount.GetScriptHash();
                    return true;
                }
            }

            if (IExpressChain.GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                accountHash = chain.GetConsensusContract().ScriptHash;
                return true;
            }

            if (UInt160.TryParse(name, out accountHash))
            {
                return true;
            }

            try
            {
                accountHash = Neo.Wallets.Helper.ToScriptHash(name, chain.AddressVersion);
                return true;
            }
            catch { }

            accountHash = UInt160.Zero;
            return false;
        }

        public static string ResolvePassword(this IExpressChain chain, string name, string password)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"{nameof(name)} parameter can't be null or empty", nameof(name));

            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password)) return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name)) return string.Empty;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase))) return string.Empty;

            // if a password is needed but not provided, prompt the user
            return McMaster.Extensions.CommandLineUtils.Prompt.GetPassword($"enter password for {name}");
        }

        public static (Neo.Wallets.Wallet wallet, UInt160 accountHash) ResolveSigner(this IExpressChain @this, string name, string password)
            => @this.TryResolveSigner(name, password, out var wallet, out var accountHash)
                ? (wallet, accountHash)
                : throw new Exception("ResolveSigner Failed");

        public static bool TryImportNEP6(this IFileSystem fileSystem, string path, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out DevWallet wallet)
        {
            if (fileSystem.File.Exists(path))
            {
                var json = Neo.IO.Json.JObject.Parse(fileSystem.File.ReadAllBytes(path));
                var nep6wallet = new Neo.Wallets.NEP6.NEP6Wallet(string.Empty, password, settings, json);

                wallet = new DevWallet(settings, nep6wallet.Name);
                foreach (var account in nep6wallet.GetAccounts())
                {
                    var devAccount = wallet.CreateAccount(account.Contract, account.GetKey());
                    devAccount.Label = account.Label;
                    devAccount.IsDefault = account.IsDefault;
                }

                return true;
            }

            wallet = null;
            return false;
        }

        public static async Task<RpcInvokeResult> GetResultAsync(this IExpressNode expressNode, Script script)
        {
            var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);
            if (result.State != VMState.HALT) throw new Exception(result.Exception ?? string.Empty);
            return result;
        }

        public static string GetNodePath(this IFileSystem fileSystem, ExpressConsensusNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(node.Wallet);

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express",
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        public static async Task<ContractParameterParser> GetContractParameterParserAsync(this IExpressNode expressNode, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            ContractParameterParser.TryGetUInt160 tryGetContract =
                (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) => TryGetContractHash(contracts, name, comparison, out scriptHash);
            return new ContractParameterParser(expressNode.ProtocolSettings, expressNode.Chain.TryResolveAccountHash, tryGetContract);
        }

        static bool TryGetContractHash(IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts, string name, StringComparison comparison, out UInt160 scriptHash)
        {
            UInt160? _scriptHash = null;
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].manifest.Name.Equals(name, comparison))
                {
                    if (_scriptHash == null)
                    {
                        _scriptHash = contracts[i].hash;
                    }
                    else
                    {
                        throw new Exception($"More than one deployed script named {name}");
                    }
                }
            }

            scriptHash = _scriptHash!;
            return _scriptHash != null;
        }
    }
}
