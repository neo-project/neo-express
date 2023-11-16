// Copyright (C) 2015-2023 The Neo Project.
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
using System.ComponentModel.DataAnnotations;
using System.Text;

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

            [Argument(0, Description = "Contract to show NFT of (symbol or script hash)")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Account to show asset balance for")]
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

                    var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, Account).ConfigureAwait(false);
                    if (getHashResult.TryPickT1(out _, out var accountHash))
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    var list = await expressNode.GetNFTAsync(accountHash, Contract).ConfigureAwait(false);
                    list.ForEach(p => console.Out.WriteLine($"TokenId: {p}, TokenId(Hex): {Encoding.UTF8.GetBytes(p).ToHexString()}"));
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
}
