using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;

namespace Neo.Express.Commands
{
    [Command("create")]
    internal class CreateCommand
    {
        [AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
        private class ValidNodeCountAttribute : ValidationAttribute
        {
            public ValidNodeCountAttribute() : base("The value for {0} must be 1, 4 or 7")
            {
            }

            protected override ValidationResult IsValid(object value, ValidationContext context)
            {
                if (value == null || (value is string str && str != "1" && str != "4" && str != "7"))
                {
                    return new ValidationResult(FormatErrorMessage(context.DisplayName));
                }

                return ValidationResult.Success;
            }
        }

        [ValidNodeCount]
        [Option]
        private int Count { get; }

        [Option]
        private string Output { get; }

        [Option]
        private bool Force { get; }

        [Option]
        private ushort Port { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var output = DevChain.GetDefaultFilename(Output);
                if (File.Exists(output) && !Force)
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }

                var nodeCount = (Count == 0 ? 1 : Count);
                var wallets = new List<(DevWallet wallet, Wallets.WalletAccount account)>();

                for (int i = 1; i <= nodeCount; i++)
                {
                    var wallet = new DevWallet($"node{i}");
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;
                    wallets.Add((wallet, account));
                }

                var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

                var contract = Neo.SmartContract.Contract.CreateMultiSigContract(keys.Length * 2 / 3 + 1, keys);

                foreach (var (wallet, account) in wallets)
                {
                    var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                    multiSigContractAccount.Label = "MultiSigContract";
                }

                var port = Port == 0 ? (ushort)49152 : Port;
                var chain = new DevChain(wallets.Select(t => new DevConsensusNode()
                {
                    Wallet = t.wallet,
                    TcpPort = port++,
                    WebSocketPort = port++,
                    RpcPort = port++
                }));

                chain.Save(output);

                console.WriteLine($"Created {nodeCount} node privatenet at {output}");
                console.WriteLine("    Note: The private keys for the accounts in this file are stored in the clear.");
                console.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
