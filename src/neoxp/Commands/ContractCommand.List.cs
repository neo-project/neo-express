using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list", Description = "List deployed contracts")]
        internal class List
        {
            readonly IExpressChain chain;

            public List(IExpressChain chain)
            {
                this.chain = chain;
            }

            public List(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out);
                    using var _ = writer.WriteArray();
                    foreach (var (hash, manifest) in contracts)
                    {
                        using var __ = writer.WriteObject();
                        writer.WriteProperty("name", manifest.Name);
                        writer.WriteProperty("hash", $"{hash}");
                    }
                }
                else
                {
                    foreach (var (hash, manifest) in contracts)
                    {
                        console.WriteLine($"{manifest.Name} ({hash})");
                    }
                }
            }
        }
    }
}
