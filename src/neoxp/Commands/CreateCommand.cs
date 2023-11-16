// Copyright (C) 2015-2023 The Neo Project.
//
// CreateCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.Network.RPC;
using static Neo.BlockchainToolkit.Constants;
using static Neo.BlockchainToolkit.Utility;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;

        public CreateCommand(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = $"Name of {EXPRESS_EXTENSION} file to create\nDefault location is home directory as:\nLinux: $HOME/.neo-express/{DEFAULT_EXPRESS_FILENAME}\nWindows: %UserProfile%\\.neo-express\\{DEFAULT_EXPRESS_FILENAME}")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create (Default: 1)")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Version to use for addresses in this blockchain instance (Default: 53)")]
        internal byte? AddressVersion { get; set; }

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        [Option(Description = "Private key for default dev account (Format: HEX or WIF)\nDefault: Random")]
        internal string PrivateKey { get; set; } = string.Empty;

        [Option(Description = "Synchronize local policy with URL of Neo JSON-RPC Node\nSpecify MainNet, TestNet or JSON-RPC URL\nDefault is not to sync")]
        internal string RpcUri { get; set; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                Models.PolicyValues? policyValues = null;

                if (string.IsNullOrEmpty(RpcUri) == false)
                {
                    if (TryParseRpcUri(RpcUri, out var uri) == false)
                        throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                    else
                    {
                        using var rpcClient = new RpcClient(uri);
                        policyValues = await rpcClient.GetPolicyAsync().ConfigureAwait(false);
                    }
                }

                byte[]? priKey = null;
                if (string.IsNullOrEmpty(PrivateKey) == false)
                {
                    try
                    {
                        if (PrivateKey.StartsWith('L'))
                            priKey = Neo.Wallets.Wallet.GetPrivateKeyFromWIF(PrivateKey);
                        else
                            priKey = Convert.FromHexString(PrivateKey);
                    }
                    catch (FormatException)
                    {
                        throw new FormatException("Private key must be in HEX or WIF format.");
                    }
                }

                var (chainManager, outputPath) = chainManagerFactory.CreateChain(Count, AddressVersion, Output, Force, privateKey: priKey);
                chainManager.SaveChain(outputPath);

                await console.Out.WriteLineAsync($"Created {Count} node privatenet at {outputPath}").ConfigureAwait(false);
                await console.Out.WriteLineAsync("    Note: The private keys for the accounts in this file are are *not* encrypted.").ConfigureAwait(false);
                await console.Out.WriteLineAsync("          Do not use these accounts on MainNet or in any other system where security is a concern.\n").ConfigureAwait(false);

                if (policyValues != null)
                {
                    using var txExec = txExecutorFactory.Create(chainManager, false, false);
                    await txExec.SetPolicyAsync(policyValues!, ExpressChainExtensions.GENESIS, string.Empty).ConfigureAwait(false);
                }

                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }
    }
}
