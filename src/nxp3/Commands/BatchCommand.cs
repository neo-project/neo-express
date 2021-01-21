using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("batch")]
    class BatchCommand
    {
        [Argument(0, Description = "path to batch file to run")]
        string BatchFile { get; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            var zapp = new CommandLineApplication();
            zapp.Command("transfer", transferCmd => {
                var asset = transferCmd.Argument("asset", "").IsRequired();
                var quantity = transferCmd.Argument("quantity", "").IsRequired();
                var sender = transferCmd.Argument("sender", "").IsRequired();
                var receiver = transferCmd.Argument("receiver", "").IsRequired();
            });
            zapp.Command("contract", contractCmd => {
                contractCmd.Command("deploy", deployCmd => {
                    var contract = deployCmd.Argument("contract", "").IsRequired();
                    var account = deployCmd.Argument("account", "").IsRequired();
                });
                contractCmd.Command("invoke", invokeCmd => {
                    var invokeFile = invokeCmd.Argument("invokeFile", "").IsRequired();
                    var account = invokeCmd.Argument("account", "").IsRequired();
                });
            });
            zapp.Command("checkpoint", checkpointCmd => {
                checkpointCmd.Command("create", createCmd => {
                    var name = createCmd.Argument("name", "").IsRequired();
                    var force = createCmd.Option<bool>("-f|--force", "", CommandOptionType.NoValue);
                    createCmd.OnExecuteAsync(async (token) => {
                        var q = name.Value;
                        var z = force.ParsedValue;
                        await Task.Delay(0);
                        return 0;
                    });
                });
            });

            // var result = zapp.Parse("checkpoint", "create", "foobar", "--force");
            var qqqqq = await zapp.ExecuteAsync(new string[]{"checkpoint", "create", "foobar", "-f"});


            var (chain, _) = Program.LoadExpressChain(Input);
            var batchapp = new CommandLineApplication<FooBatch>();
            batchapp.Conventions.UseDefaultConventions();
            var qqq = batchapp.Parse("checkpoint", "create", "foobar", "--force");
            

            await Task.FromResult(0);
            return -1;
        }

        [Command]
        [Subcommand(typeof(FooCheckpoint), typeof(FooContract), typeof(FooTransfer))]
        class FooBatch
        {
        }

        [Command("transfer")]
        class FooTransfer
        {
            [Argument(0, Description = "Asset to transfer (symbol or script hash)")]
            [Required]
            string Asset { get; } = string.Empty;

            [Argument(1, Description = "Amount to transfer")]
            [Required]
            string Quantity { get; } = string.Empty;

            [Argument(2, Description = "Account to send asset from")]
            [Required]
            string Sender { get; } = string.Empty;

            [Argument(3, Description = "Account to send asset to")]
            [Required]
            string Receiver { get; } = string.Empty;
        }

        [Command("contract")]
        [Subcommand(typeof(FooContract.Deploy), typeof(FooContract.Invoke))]
        class FooContract
        {
            [Command("deploy")]
            class Deploy
            {
                [Argument(0, Description = "Path to contract .nef file")]
                [Required]
                string Contract { get; } = string.Empty;

                [Argument(1, Description = "Account to pay contract deployment GAS fee")]
                [Required]
                string Account { get; } = string.Empty;
            }

            [Command("invoke")]
            class Invoke
            {
                [Argument(0, Description = "Path to contract invocation JSON file")]
                [Required]
                string InvocationFile { get; } = string.Empty;

                [Argument(1, Description = "Account to pay contract invocation GAS fee")]
                [Required]
                string Account { get; } = string.Empty;
            }
        }

        [Command("checkpoint")]
        [Subcommand(typeof(FooCheckpoint.Create))]
        class FooCheckpoint
        {
            [Command("create")]
            class Create
            {
                [Argument(0, "Checkpoint file name")]
                string Name { get; } = string.Empty;

                [Option(Description = "Overwrite existing data")]
                bool Force { get; }
            }
        }
    }
}
