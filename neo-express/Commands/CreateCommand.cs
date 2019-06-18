using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Neo.Express.Commands
{
    [Command("create")]
    class CreateCommand
    {
        class ValidNodeCountAttribute : ValidationAttribute
        {
            public ValidNodeCountAttribute() : base("The value for {0} must be '4' or '7'")
            {
            }

            protected override ValidationResult IsValid(object value, ValidationContext context)
            {
                if (value == null || (value is string str && str != "4" && str != "7"))
                {
                    return new ValidationResult(FormatErrorMessage(context.DisplayName));
                }

                return ValidationResult.Success;
            }
        }

        [ValidNodeCount]
        [Option]
        int MultiNode { get; }

        async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            await Task.Delay(1);

            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>();

            for (int i = 1; i <= (MultiNode == 0 ? 1 : MultiNode); i++)
            {
                var wallet = new DevWallet($"node{i}");
                var account = wallet.CreateAccount();
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

            var contract = Neo.SmartContract.Contract.CreateMultiSigContract(keys.Length * 2 / 3 + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                wallet.CreateAccount(contract, account.GetKey());
            }

            using (var stream = Console.OpenStandardOutput())
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartArray();
                foreach (var (wallet, _) in wallets)
                {
                    wallet.WriteJson(writer);
                }
                writer.WriteEndArray();
            }

            return 0;
        }
    }
}
