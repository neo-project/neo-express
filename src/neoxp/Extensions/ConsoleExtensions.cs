using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class ConsoleExtensions
    {
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

        public static void WriteException(this CommandLineApplication app, Exception exception, bool showInnerExceptions = false)
        {
            var showStackTrace = ((CommandOption<bool>)app.GetOptions().Single(o => o.LongName == "stack-trace")).ParsedValue;

            app.Error.WriteLine($"\x1b[1m\x1b[31m\x1b[40m{exception.GetType()}: {exception.Message}\x1b[0m");

            if (showStackTrace) app.Error.WriteLine($"\x1b[1m\x1b[37m\x1b[40m{exception.StackTrace}\x1b[0m");

            if (showInnerExceptions || showStackTrace)
            {
                while (exception.InnerException is not null)
                {
                    app.Error.WriteLine($"\x1b[1m\x1b[33m\x1b[40m\tInner {exception.InnerException.GetType().Name}: {exception.InnerException.Message}\x1b[0m");
                    exception = exception.InnerException;
                }
            }
        }

        public static void WriteTxHash(this TextWriter writer, UInt256 txHash, string txType = "", bool json = false)
        {
            if (json)
            {
                writer.WriteLine($"{txHash}");
            }
            else
            {
                if (!string.IsNullOrEmpty(txType)) writer.Write($"{txType} ");
                writer.WriteLine($"Transaction {txHash} submitted");
            }
        }

        public static void WriteJson(this IConsole console, Neo.IO.Json.JObject json)
        {
            using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out)
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            writer.WriteJson(json);
            console.Out.WriteLine();
        }


        public static void WriteWallet(this TextWriter writer, ExpressWallet wallet)
        {
            writer.WriteLine(wallet.Name);

            foreach (var account in wallet.Accounts)
            {
                WriteAccount(writer, account);
            }

            static void WriteAccount(TextWriter writer, ExpressWalletAccount account)
            {
                var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
                var address = account.ScriptHash;
                var scriptHash = Neo.IO.Helper.ToArray(account.CalculateScriptHash());

                writer.WriteLine($"  {address} ({(account.IsDefault ? "Default" : account.Label)})");
                writer.WriteLine($"    script hash: {BitConverter.ToString(scriptHash)}");
                writer.WriteLine($"    public key:  {Convert.ToHexString(keyPair.PublicKey.EncodePoint(true))}");
                writer.WriteLine($"    private key: {Convert.ToHexString(keyPair.PrivateKey)}");
            }
        }

        public static void WriteResult(this TextWriter @this, RpcInvokeResult result, bool json)
        {
            if (json)
            {
                using var writer = new JsonTextWriter(@this) { Formatting = Formatting.Indented };
                writer.WriteJson(result.ToJson());
            }
            else
            {
                @this.WriteLine($"VM State:     {result.State}");
                @this.WriteLine($"Gas Consumed: {result.GasConsumed}");
                if (!string.IsNullOrEmpty(result.Exception))
                {
                    @this.WriteLine($"Exception:   {result.Exception}");
                }
                if (result.Stack.Length > 0)
                {
                    var stack = result.Stack;
                    @this.WriteLine("Result Stack:");
                    for (int i = 0; i < stack.Length; i++)
                    {
                        @this.WriteStackItem(stack[i]);
                    }
                }
            }
        }

        public static void WriteStackItem(this TextWriter writer, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    WriteLine(item.GetBoolean() ? "true" : "false");
                    break;
                case Neo.VM.Types.Integer @int:
                    WriteLine($"{@int.GetInteger()}");
                    break;
                case Neo.VM.Types.Buffer buffer:
                    WriteLine(Convert.ToHexString(buffer.GetSpan()));
                    break;
                case Neo.VM.Types.ByteString byteString:
                    WriteLine(Convert.ToHexString(byteString.GetSpan()));
                    break;
                case Neo.VM.Types.Null _:
                    WriteLine($"<null>");
                    break;
                case Neo.VM.Types.Array array:
                    WriteLine($"{(array is Neo.VM.Types.Struct ? "Struct" : "Array")}: ({array.Count})");
                    for (int i = 0; i < array.Count; i++)
                    {
                        WriteStackItem(writer, array[i], indent + 1);
                    }
                    break;
                case Neo.VM.Types.Map map:
                    WriteLine($"Array: ({map.Count})");
                    foreach (var m in map)
                    {
                        WriteStackItem(writer, m.Key, indent + 1, "key:   ");
                        WriteStackItem(writer, m.Value, indent + 1, "value: ");
                    }
                    break;
            }

            void WriteLine(string value)
            {
                for (var i = 0; i < indent; i++)
                {
                    writer.Write("  ");
                }

                if (!string.IsNullOrEmpty(prefix))
                {
                    writer.Write(prefix);
                }

                writer.WriteLine(value);
            }
        }
    }
}