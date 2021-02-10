using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "invoke")]
        internal class Invoke
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly IFileSystem fileSystem;

            public Invoke(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Path to contract invocation JSON file")]
            [Required]
            string InvocationFile { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
            string Account { get; } = string.Empty;

            [Option("--test", Description = "Test invocation (does not cost GAS)")]
            bool Test { get; } = false;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
            decimal AdditionalGas { get; } = 0;

            [Option(Description = "Enable contract execution tracing")]
            bool Trace { get; } = false;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal static async Task ExecuteTxAsync(IExpressChainManager chainManager, IExpressNode expressNode, string invocationFile, string account, IFileSystem fileSystem, System.IO.TextWriter writer, bool json = false)
            {
                if (!fileSystem.File.Exists(invocationFile))
                {
                    throw new Exception($"Invocation file {invocationFile} couldn't be found");
                }

                var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
                var script = await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);

                var _account = chainManager.Chain.GetAccount(account) ?? throw new Exception($"{account} account not found.");
                var txHash = await expressNode.ExecuteAsync(_account, script).ConfigureAwait(false);
                if (json)
                {
                    await writer.WriteLineAsync($"{txHash}").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync($"Invocation Transaction {txHash} submitted").ConfigureAwait(false);
                }
            }

            internal static async Task ExecuteTestAsync(IExpressChainManager chainManager, IExpressNode expressNode, string invocationFile, IFileSystem fileSystem, System.IO.TextWriter writer, bool json = false)
            {
                if (!fileSystem.File.Exists(invocationFile))
                {
                    throw new Exception($"Invocation file {invocationFile} couldn't be found");
                }

                var parser = await expressNode.GetContractParameterParserAsync(chainManager).ConfigureAwait(false);
                var script = await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);

                var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);
                if (json)
                {
                    await writer.WriteLineAsync(result.ToJson().ToString(true)).ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync($"VM State:     {result.State}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"Gas Consumed: {result.GasConsumed}").ConfigureAwait(false);
                    if (result.Exception != null)
                    {
                        await writer.WriteLineAsync($"Expception:   {result.Exception}").ConfigureAwait(false);
                    }
                    if (result.Stack.Length > 0)
                    {
                        var stack = result.Stack;
                        await writer.WriteLineAsync("Result Stack:").ConfigureAwait(false);
                        for (int i = 0; i < stack.Length; i++)
                        {
                            await WriteStackItemAsync(writer, stack[i]).ConfigureAwait(false);
                        }
                    }
                }
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    if (Test)
                    {
                        await ExecuteTestAsync(chainManager, expressNode, InvocationFile, fileSystem, console.Out, Json);
                    }
                    else
                    {
                        await ExecuteTxAsync(chainManager, expressNode, InvocationFile, Account, fileSystem, console.Out, Json);
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync($"{ex.Message} [{ex.GetType().Name}]").ConfigureAwait(false);
                    while (ex.InnerException != null)
                    {
                        await console.Error.WriteLineAsync($"  Contract Exception: {ex.InnerException.Message} [{ex.InnerException.GetType().Name}]").ConfigureAwait(false);
                        ex = ex.InnerException;
                    }
                    return 1;
                }
            }

            static async Task WriteStackItemAsync(System.IO.TextWriter writer, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
            {
                switch (item)
                {
                    case Neo.VM.Types.Boolean _:
                        await WriteLineAsync(item.GetBoolean() ? "true" : "false").ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Integer @int:
                        await WriteLineAsync(@int.GetInteger().ToString()).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Buffer buffer:
                        await WriteLineAsync(Neo.Helper.ToHexString(buffer.GetSpan())).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.ByteString byteString:
                        await WriteLineAsync(Neo.Helper.ToHexString(byteString.GetSpan())).ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Null _:
                        await WriteLineAsync("<null>").ConfigureAwait(false);
                        break;
                    case Neo.VM.Types.Array array:
                        await WriteLineAsync($"Array: ({array.Count})").ConfigureAwait(false);
                        for (int i = 0; i < array.Count; i++)
                        {
                            await WriteStackItemAsync(writer, array[i], indent + 1).ConfigureAwait(false);
                        }
                        break;
                    case Neo.VM.Types.Map map:
                        await WriteLineAsync($"Map: ({map.Count})").ConfigureAwait(false);
                        foreach (var m in map)
                        {
                            await WriteStackItemAsync(writer, m.Key, indent + 1, "key:   ").ConfigureAwait(false);
                            await WriteStackItemAsync(writer, m.Value, indent + 1, "value: ").ConfigureAwait(false);
                        }
                        break;
                }

                async Task WriteLineAsync(string value)
                {
                    for (var i = 0; i < indent; i++)
                    {
                        await writer.WriteAsync("  ").ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        await writer.WriteAsync(prefix).ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync(value).ConfigureAwait(false);
                }
            }
        }
    }
}
