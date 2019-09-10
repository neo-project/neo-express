using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace NeoExpress.Neo2Backend
{
    internal static class NeoUtility
    {
        public static (IEnumerable<UInt160> hashes, byte[] data) ParseResultHashesAndData(Newtonsoft.Json.Linq.JToken result)
        {
            var hashes = result["script-hashes"].Select(t => ((string)t).ToScriptHash());
            var data = result.Value<string>("hash-data").HexToBytes();
            return (hashes, data);
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

        static ScriptBuilder BuildInvocationScript(UInt160 scriptHash, ContractParameter[] parameters)
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
