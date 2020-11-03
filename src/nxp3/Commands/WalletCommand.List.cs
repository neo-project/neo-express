using System;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;

namespace nxp3.Commands
{
    partial class WalletCommand
    {
        [Command("list")]
        class List
        {
            [Option]
            string Input { get; } = string.Empty;

            [Option]
            bool Dev { get; }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();

                    var wallets = chain.ConsensusNodes.Select(n => n.Wallet)
                        .Concat(chain.Wallets ?? Enumerable.Empty<ExpressWallet>());
                    foreach (var wallet in wallets)
                    {
                        console.WriteLine(wallet.Name);

                        foreach (var account in wallet.Accounts)
                        {
                            console.WriteLine($"  {account.ScriptHash} ({(account.IsDefault ? "Default" : account.Label)})");
                            if (Dev)
                            {
                                var scriptHash = blockchainOperations.ToScriptHashByteArray(account);
                                console.Write("    C#: { ");
                                foreach (var b in scriptHash)
                                {
                                    console.Write($"0x{b.ToString("x2")}, ");
                                }
                                console.WriteLine("}");
                                console.Write("    Python: b'");
                                foreach (var b in scriptHash)
                                {
                                    console.Write($"\\x{b.ToString("x2")}");
                                }
                                console.WriteLine("'");
                            }
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
