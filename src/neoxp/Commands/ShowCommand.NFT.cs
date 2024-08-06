// Copyright (C) 2015-2024 The Neo Project.
//
// ShowCommand.NFT.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Extensions;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("nft", Description = "Show NFT Tokens for account")]
        internal class NFT
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public NFT(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "NFT Contract (Symbol or Script Hash)")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Account to show NFT (Format: Script Hash, Address, Wallet name)")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    if (!UInt160.TryParse(Account, out var accountHash)) //script hash
                    {
                        if (!chainManager.Chain.TryParseScriptHash(Account, out accountHash)) //address
                        {
                            var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, Account).ConfigureAwait(false); //wallet name
                            if (getHashResult.TryPickT1(out _, out accountHash))
                            {
                                throw new Exception($"{Account} account not found.");
                            }
                        }
                    }
                    var parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
                    var scriptHash = parser.TryLoadScriptHash(Contract, out var value)
                        ? value
                        : UInt160.TryParse(Contract, out var uint160)
                            ? uint160
                            : throw new InvalidOperationException($"contract \"{Contract}\" not found");
                    var list = await expressNode.GetNFTAsync(accountHash, scriptHash).ConfigureAwait(false);
                    if (list.Count == 0)
                        await console.Out.WriteLineAsync($"No NFT yet. (Contract:{scriptHash}, Account:{accountHash.ToAddress(ProtocolSettings.Default.AddressVersion)})");
                    else
                        list.ForEach(p => console.Out.WriteLine($"TokenId(Base64): {p}, TokenId(Hex): 0x{Convert.FromBase64String(p).Reverse().ToArray().ToHexString()}"));
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex, true);
                    return 1;
                }
            }
        }
    }
}
