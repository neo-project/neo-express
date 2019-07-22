using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Neo.Express
{
    internal static class NeoUtility
    {
        public static JObject Sign(this WalletAccount account, byte[] data)
        {
            var key = account.GetKey();
            var signature = Cryptography.Crypto.Default.Sign(data, key.PrivateKey,
                key.PublicKey.EncodePoint(false).Skip(1).ToArray());

            return new JObject
            {
                ["signature"] = signature.ToHexString(),
                ["public-key"] = key.PublicKey.EncodePoint(true).ToHexString(),
                ["contract"] = new JObject
                {
                    ["script"] = account.Contract.Script.ToHexString(),
                    ["parameters"] = new JArray(account.Contract.ParameterList.Select(cpt => Enum.GetName(typeof(ContractParameterType), cpt)))
                }
            };
        }

        public static IEnumerable<JObject> Sign(this DevWallet wallet, IEnumerable<UInt160> hashes, byte[] data)
        {
            foreach (var hash in hashes)
            {
                var account = wallet.GetAccount(hash);
                if (account == null || !account.HasKey)
                    continue;

                yield return Sign(account, data);
            }
        }

        public static (IEnumerable<UInt160> hashes, byte[] data) ParseResultHashesAndData(JToken result)
        {
            var hashes = result["script-hashes"].Select(t => t.Value<string>().ToScriptHash());
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

    }
}
