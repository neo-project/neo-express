using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Node
{
    internal static class NodeUtility
    {
        public static Task RunAsync(Store store, ExpressConsensusNode node, TextWriter writer, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    using (var system = new NeoSystem(store))
                    {
                        var logPlugin = new LogPlugin(writer);
                        var rpcPlugin = new ExpressNodeRpcPlugin();

                        system.StartNode(node.TcpPort, node.WebSocketPort);
                        system.StartConsensus(wallet);
                        system.StartRpc(IPAddress.Loopback, node.RpcPort, wallet);
                        StartDebug(new IPEndPoint(IPAddress.Loopback, node.DebugPort), writer, cancellationToken);

                        cancellationToken.WaitHandle.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    if (store is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                    tcs.TrySetResult(true);
                }
            });

            return tcs.Task;
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        private static void StartDebug(IPEndPoint endPoint, TextWriter writer, CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                writer.WriteLine($"DEBUGGER listening on {endPoint}");
                var listener = new TcpListener(endPoint);
                listener.Start();

                // TcpListener.AcceptSocketAsync doesn't support CancellationToken, so Stop the listener when 
                // cancellationToken is triggered
                using (cancellationToken.Register(() => listener.Stop()))
                {
                    while (true)
                    {
                        var clientSocket = await listener.AcceptSocketAsync().WithCancellation(cancellationToken);

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            using (clientSocket)
                            using (var stream = new NetworkStream(clientSocket))
                            {
                                var adapter = new NeoDebug.DebugAdapter(stream, stream,
                                    DebugExecutionEngine.CreateExecutionEngine,
                                    Neo.Cryptography.Crypto.Default.Hash160,
                                    (cat, msg) => writer.WriteLine($"{DateTimeOffset.Now.ToString("HH:mm:ss.ff")} DEBUGGER {cat} {msg}"));

                                // DebugAdapter.Run doesn't support CancellationToken, so Stop the Protocol when 
                                // cancellationToken is triggered
                                using (cancellationToken.Register(() => adapter.Protocol.Stop()))
                                {
                                    adapter.Run();
                                    adapter.Protocol.WaitForReader();
                                }
                            }
                        });
                    }
                }
            });
        }

        private static bool CoinUnspent(Coin c)
        {
            return (c.State & CoinState.Confirmed) != 0
                && (c.State & CoinState.Spent) == 0
                && (c.State & CoinState.Claimed) == 0
                && (c.State & CoinState.Frozen) == 0;
        }

        public static bool CoinUnclaimed(Coin c)
        {
            return (c.State & CoinState.Confirmed) != 0
                && (c.State & CoinState.Spent) != 0
                && (c.State & CoinState.Claimed) == 0
                && (c.State & CoinState.Frozen) == 0;
        }

        public static IEnumerable<Coin> GetCoins(Snapshot snapshot, ImmutableHashSet<UInt160> addresses)
        {
            var coinIndex = new Dictionary<CoinReference, Coin>();
            var height = snapshot.Height;

            for (uint blockIndex = 0; blockIndex < height; blockIndex++)
            {
                var block = snapshot.GetBlock(blockIndex);

                for (var txIndex = 0; txIndex < block.Transactions.Length; txIndex++)
                {
                    var tx = block.Transactions[txIndex];

                    for (var outIndex = 0; outIndex < tx.Outputs.Length; outIndex++)
                    {
                        var output = tx.Outputs[outIndex];

                        if (addresses.Contains(output.ScriptHash))
                        {
                            var coinRef = new CoinReference()
                            {
                                PrevHash = tx.Hash,
                                PrevIndex = (ushort)outIndex
                            };

                            coinIndex.Add(coinRef, new Coin()
                            {
                                Reference = coinRef,
                                Output = output,
                                State = CoinState.Confirmed
                            });
                        }
                    }

                    for (var inIndex = 0; inIndex < tx.Inputs.Length; inIndex++)
                    {
                        if (coinIndex.TryGetValue(tx.Inputs[inIndex], out var coin))
                        {
                            coin.State |= CoinState.Spent | CoinState.Confirmed;
                        }
                    }

                    if (tx is ClaimTransaction claimTx)
                    {
                        for (var claimIndex = 0; claimIndex < claimTx.Claims.Length; claimIndex++)
                        {
                            if (coinIndex.TryGetValue(claimTx.Claims[claimIndex], out var coin))
                            {
                                coin.State |= CoinState.Claimed;
                            }
                        }
                    }
                }
            }

            return coinIndex.Select(kvp => kvp.Value);
        }

        public static IEnumerable<Coin> Unspent(this IEnumerable<Coin> coins, UInt256 assetId = null)
        {
            var ret = coins.Where(CoinUnspent);
            if (assetId != null)
            {
                ret = ret.Where(c => c.Output.AssetId == assetId);
            }
            return ret;
        }

        public static IEnumerable<Coin> Unclaimed(this IEnumerable<Coin> coins, UInt256 assetId = null)
        {
            var ret = coins.Where(CoinUnclaimed);
            if (assetId != null)
            {
                ret = ret.Where(c => c.Output.AssetId == assetId);
            }
            return ret;
        }

        private static IEnumerable<(Coin coin, Fixed8 amount)> GetInputs(IEnumerable<Coin> coins, UInt256 assetId, Fixed8 amount)
        {
            var assets = coins.Where(c => c.Output.AssetId == assetId)
                .OrderByDescending(c => c.Output.Value);

            foreach (var coin in assets)
            {
                if (amount < coin.Output.Value)
                {
                    yield return (coin, amount);
                }
                else
                {
                    yield return (coin, coin.Output.Value);
                }

                amount -= coin.Output.Value;
                if (amount <= Fixed8.Zero)
                {
                    break;
                }
            }
        }

        private static IEnumerable<TransactionOutput> GetOutputs(IEnumerable<(Coin coin, Fixed8 amount)> inputs)
        {
            return inputs
                .Where(t => t.amount < t.coin.Output.Value)
                .Select(t => new TransactionOutput
                {
                    AssetId = t.coin.Output.AssetId,
                    ScriptHash = t.coin.Output.ScriptHash,
                    Value = t.coin.Output.Value - t.amount
                });
        }

        public static ContractTransaction MakeTransferTransaction(Snapshot snapshot,
            ImmutableHashSet<UInt160> senderAddresses,
            UInt160 receiver, UInt256 assetId, Fixed8? amount = null)
        {
            var coins = GetCoins(snapshot, senderAddresses)
                .Unspent(assetId);

            if (coins == null)
            {
                return null;
            }

            var sum = coins.Sum(c => c.Output.Value);

            if (!amount.HasValue)
            {
                return new ContractTransaction
                {
                    Inputs = coins.Select(c => c.Reference).ToArray(),
                    Outputs = new TransactionOutput[] {
                        new TransactionOutput
                        {
                            AssetId = assetId,
                            Value = sum,
                            ScriptHash = receiver
                        }
                    },
                    Attributes = new TransactionAttribute[0],
                    Witnesses = new Witness[0],
                };
            }

            if (sum < amount.Value)
            {
                return null;
            }

            var inputs = GetInputs(coins, assetId, amount.Value);
            var outputs = GetOutputs(inputs).Append(new TransactionOutput
            {
                AssetId = assetId,
                Value = amount.Value,
                ScriptHash = receiver
            });

            return new ContractTransaction
            {
                Inputs = inputs.Select(t => t.coin.Reference).ToArray(),
                Outputs = outputs.ToArray(),
                Attributes = new TransactionAttribute[0],
                Witnesses = new Witness[0],
            };
        }

        public static ClaimTransaction MakeClaimTransaction(Snapshot snapshot, UInt160 address, UInt256 assetId)
        {
            var coinReferences = GetCoins(snapshot, ImmutableHashSet.Create(address))
                .Unclaimed(assetId)
                .Take(50)
                .Select(c => c.Reference);

            if (coinReferences.Any())
            {
                return new ClaimTransaction
                {
                    Claims = coinReferences.ToArray(),
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = Blockchain.UtilityToken.Hash,
                            Value = snapshot.CalculateBonus(coinReferences),
                            ScriptHash = address
                        }
                    }
                };
            }

            return null;
        }

        public static (InvocationTransaction, ApplicationEngine) MakeDeploymentTransaction(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, Newtonsoft.Json.Linq.JToken contract)
        {
            var tx = BuildInvocationTx(() => BuildContractCreateScript(contract));
            var engine = ApplicationEngine.Run(tx.Script, tx, null, true);
            if ((engine.State & VMState.FAULT) != 0)
            {
                throw new Exception();
            }

            var gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            tx.Gas = (gas < Fixed8.Zero) ? Fixed8.Zero : gas.Ceiling();

            return (AddTransactionFee(snapshot, addresses, tx), engine);
        }

        public static (InvocationTransaction, ApplicationEngine) MakeInvocationTransaction(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, UInt160 scriptHash, ContractParameter[] parameters)
        {
            var tx = BuildInvocationTx(() => BuildInvocationScript(scriptHash, parameters));
            var engine = ApplicationEngine.Run(tx.Script, tx);
            if ((engine.State & VMState.FAULT) != 0)
            {
                throw new Exception();
            }

            return addresses.IsEmpty ? (null, engine) : (AddTransactionFee(snapshot, addresses, tx), engine);
        }

        private static InvocationTransaction AddTransactionFee(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, InvocationTransaction tx)
        {
            var fee = Fixed8.Zero;
            if (tx.Size > 1024)
            {
                fee = Fixed8.FromDecimal(0.001m);
                fee += Fixed8.FromDecimal(tx.Size * 0.00001m);
            }
            fee += tx.SystemFee;

            var coins = GetCoins(snapshot, addresses).Unspent(Blockchain.UtilityToken.Hash);
            var sum = coins.Sum(c => c.Output.Value);
            if (sum < fee)
            {
                return null;
            }

            var inputs = GetInputs(coins, Blockchain.UtilityToken.Hash, fee);
            var outputs = GetOutputs(inputs);

            tx.Inputs = tx.Inputs.Concat(inputs.Select(t => t.coin.Reference)).ToArray();
            tx.Outputs = tx.Outputs.Concat(outputs).ToArray();

            return tx;
        }

        private static InvocationTransaction BuildInvocationTx(Func<ScriptBuilder> func)
        {
            using (var builder = func())
            {
                return new InvocationTransaction
                {
                    Version = 1,
                    Script = builder.ToArray(),
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = new TransactionOutput[0],
                    Witnesses = new Witness[0],
                };
            }
        }

        private static ScriptBuilder BuildInvocationScript(UInt160 scriptHash, ContractParameter[] parameters)
        {
            var builder = new ScriptBuilder();
            builder.EmitAppCall(scriptHash, parameters);
            return builder;
        }

        private static ScriptBuilder BuildContractCreateScript(JToken json)
        {
            ContractParameterType TypeParse(JToken jtoken) => Enum.Parse<ContractParameterType>(jtoken.Value<string>());

            var contractData = json.Value<string>("contract-data").HexToBytes();

            var entryPoint = json.Value<string>("entry-point");
            var entryFunction = json["functions"].Single(t => t.Value<string>("name") == entryPoint);
            var entryParameters = entryFunction["parameters"].Select(t => TypeParse(t.Value<string>("type")));
            var entryReturnType = TypeParse(entryFunction["return-type"]);

            var props = json["properties"];
            var title = props.Value<string>("title") ?? json.Value<string>("name");
            var description = json.Value<string>("description") ?? "no description provided";
            var version = json.Value<string>("version") ?? "0.1.0";
            var author = json.Value<string>("author") ?? "no description provided";
            var email = json.Value<string>("email") ?? "nobody@fake.email";

            var contractPropertyState = ContractPropertyState.NoProperty;
            if (props.Value<bool?>("has-storage") == true) contractPropertyState |= ContractPropertyState.HasStorage;
            if (props.Value<bool?>("has-dynamic-invoke") == true) contractPropertyState |= ContractPropertyState.HasDynamicInvoke;

            var builder = new ScriptBuilder();
            builder.EmitSysCall("Neo.Contract.Create",
                contractData,
                entryParameters.ToArray(),
                entryReturnType,
                contractPropertyState,
                title,
                version,
                author,
                email,
                description);
            return builder;
        }
    }
}
