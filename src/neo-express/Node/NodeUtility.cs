using Akka.Actor;
using Neo;
using Neo.Cryptography;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Node
{
    internal static class NodeUtility
    {
        static ulong NextNonce(Random random)
        {
            Span<byte> nonceSpan = stackalloc byte[sizeof(ulong)];
            random.NextBytes(nonceSpan);
            return BinaryPrimitives.ReadUInt64LittleEndian(nonceSpan);
        }

        // Since ConensusContext's constructor is internal, it can't be used from neo-express.
        // CreatePreloadBlock replicates the following logic for creating an empty block with ConensusContext

        // var ctx = new Neo.Consensus.ConsensusContext(wallet, store);
        // ctx.Reset(0);
        // ctx.MakePrepareRequest();
        // ctx.MakeCommit();
        // ctx.Save();
        // Block block = ctx.CreateBlock();

        static Block CreatePreloadBlock(Neo.Wallets.Wallet wallet, Random random, Transaction? transaction = null)
        {
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            var validators = snapshot.GetValidators();
            if (validators.Length != 1)
            {
                throw new InvalidOperationException("Preload only supported for single-node blockchains");
            }

            var amountNetFee = Block.CalculateNetFee(Enumerable.Empty<Transaction>());
            if (amountNetFee != Fixed8.Zero)
            {
                throw new InvalidOperationException("amountNetFee must be zero");
            }

            var keyPair = wallet.GetAccount(validators[0]).GetKey();
            var prevHash = snapshot.CurrentBlockHash;
            var prevBlock = snapshot.GetBlock(prevHash);
            var nonce = NextNonce(random);

            var minerTx = new MinerTransaction
            {
                Nonce = (uint)(nonce % (uint.MaxValue + 1ul)),
                Attributes = Array.Empty<TransactionAttribute>(),
                Inputs = Array.Empty<CoinReference>(),
                Outputs = Array.Empty<TransactionOutput>(),
                Witnesses = Array.Empty<Witness>()
            };

            var blockTransactions = transaction == null ? new Transaction[] { minerTx } : new Transaction[] { minerTx, transaction };
            var txHashes = blockTransactions.Select(tx => tx.Hash).ToArray();
            var merkleRoot = MerkleTree.ComputeRoot(txHashes);
            var nextConsensus = Blockchain.GetConsensusAddress(snapshot.GetValidators(blockTransactions).ToArray());
            var consensusData = nonce;

            var block = new Block()
            {
                Version = 0,
                PrevHash = prevHash,
                MerkleRoot = merkleRoot,
                Timestamp = prevBlock.Timestamp + 1,
                Index = prevBlock.Index + 1,
                ConsensusData = nonce,
                NextConsensus = nextConsensus,
                Transactions = Array.Empty<Transaction>(),
            };

            var commit = new Neo.Consensus.Commit()
            {
                ViewNumber = 0,
                Signature = block.Sign(keyPair)
            };
            var payload = new ConsensusPayload
            {
                Version = 0,
                PrevHash = prevHash,
                BlockIndex = block.Index,
                ValidatorIndex = (ushort)0,
                Data = Neo.IO.Helper.ToArray(commit)
            };

            {
                var sc = new ContractParametersContext(payload);
                wallet.Sign(sc);
                payload.Witness = sc.GetWitnesses()[0];
            }

            {
                var m = validators.Length - ((validators.Length - 1) / 3);
                Contract contract = Contract.CreateMultiSigContract(m, validators);
                ContractParametersContext sc = new ContractParametersContext(block);
                for (int i = 0, j = 0; i < validators.Length && j < m; i++)
                {
                    sc.AddSignature(contract, validators[0], payload.GetDeserializedMessage<Neo.Consensus.Commit>().Signature);
                    j++;
                }
                block.Witness = sc.GetWitnesses()[0];
                block.Transactions = blockTransactions;
            }
            return block;
        }

        public static void ClaimPreloadGas(NeoSystem system, Neo.Wallets.Wallet wallet, Random random)
        {
            void SubmitTransaction(Func<Snapshot, WalletAccount, Transaction?> factory)
            {
                using var snapshot = Blockchain.Singleton.GetSnapshot();
                var validators = snapshot.GetValidators();
                if (validators.Length != 1)
                {
                    throw new InvalidOperationException("Preload only supported for single-node blockchains");
                }

                var account = wallet.GetAccounts().Single(a => a.Contract.Script.IsMultiSigContract());

                var tx = factory(snapshot, account);
                if (tx == null)
                {
                    throw new Exception("Attempted to submit null preload transaction");
                }
                var context = new ContractParametersContext(tx);
                wallet.Sign(context);
                if (!context.Completed)
                {
                    throw new InvalidOperationException("could not complete signing of preload transaction");
                }

                var block = CreatePreloadBlock(wallet, random, tx);
                var result = system.Blockchain.Ask<RelayResultReason>(block).Result;

                if (result != RelayResultReason.Succeed)
                {
                    throw new Exception($"Preload {tx.Type} transaction failed {result}");
                }
            }

            SubmitTransaction((snapshot, account) => NodeUtility.MakeTransferTransaction(snapshot,
                ImmutableHashSet.Create(account.ScriptHash), account.ScriptHash, Blockchain.GoverningToken.Hash, null));

            // There needs to be least one block after the transfer transaction before submitting a GAS claim
            var block = CreatePreloadBlock(wallet, random);
            var result = system.Blockchain.Ask<RelayResultReason>(block).Result;
            if (result != RelayResultReason.Succeed)
            {
                throw new Exception($"Preload transfer suffix block failed {result}");
            }

            SubmitTransaction((snapshot, account) => NodeUtility.MakeClaimTransaction(snapshot, account.ScriptHash,
                Blockchain.GoverningToken.Hash));
        }

        public static Task RunAsync(Store store, ExpressConsensusNode node, TextWriter writer, uint preloadGas, CancellationToken cancellationToken)
        {
            static bool PreloadValid()
            {
                using var snapshot = Blockchain.Singleton.GetSnapshot();
                var lastBlock = Blockchain.Singleton.GetBlock(snapshot.CurrentBlockHash);
                var validators = snapshot.GetValidators();
                return lastBlock.Index == 0 && validators.Length == 1;
            }

            static uint CalculateGas(uint preloadGas)
            {
                var generationAmount = Blockchain.GenerationAmount[0];
                var gas = preloadGas / generationAmount;
                return preloadGas % generationAmount == 0 ? gas : gas + 1;
            }

            var tcs = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    using (var system = new NeoSystem(store))
                    {
                        var logPlugin = new LogPlugin(writer);
                        var rpcPlugin = new ExpressNodeRpcPlugin(store);

                        system.StartNode(node.TcpPort, node.WebSocketPort);

                        if (PreloadValid() && preloadGas > 0)
                        {
                            var preloadCount = CalculateGas(preloadGas);
                            Plugin.Log("neo-express", LogLevel.Info, $"Creating {preloadCount} empty blocks to preload {preloadGas} GAS");
                            Random random = new Random();
                            for (int i = 1; i <= preloadCount; i++)
                            {
                                if (i % 100 == 0)
                                {
                                    Plugin.Log("neo-express", LogLevel.Info, $"Creating Block {i}");
                                }
                                var block = CreatePreloadBlock(wallet, random);
                                var relayResult = system.Blockchain.Ask<RelayResultReason>(block).Result;

                                if (relayResult != RelayResultReason.Succeed)
                                {
                                    throw new Exception($"Preload block {i} failed {relayResult}");
                                }
                            }

                            ClaimPreloadGas(system, wallet, random);
                        }
                        
                        system.StartConsensus(wallet);
                        system.StartRpc(IPAddress.Loopback, node.RpcPort, wallet);

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

        public static UInt256 GetAssetId(string asset)
        {
            if (string.Compare("neo", asset, true) == 0)
                return Blockchain.GoverningToken.Hash;

            if (string.Compare("gas", asset, true) == 0)
                return Blockchain.UtilityToken.Hash;

            return UInt256.Parse(asset);
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

        public static IEnumerable<Coin> Unspent(this IEnumerable<Coin> coins, UInt256? assetId = null)
        {
            var ret = coins.Where(CoinUnspent);
            if (assetId != null)
            {
                ret = ret.Where(c => c.Output.AssetId == assetId);
            }
            return ret;
        }

        public static IEnumerable<Coin> Unclaimed(this IEnumerable<Coin> coins, UInt256? assetId = null)
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

        public static (Fixed8 generated, Fixed8 sysFee) CalculateClaimable(Snapshot snapshot, Fixed8 value, uint startHeight, uint endHeight)
        {
            static long GetSysFeeAmountForHeight(Snapshot snapshot, uint height)
            {
                return snapshot.Blocks.TryGet(Blockchain.Singleton.GetBlockHash(height)).SystemFeeAmount;
            }

            var decrementInterval = Blockchain.DecrementInterval;
            var generationAmountLength = Blockchain.GenerationAmount.Length;

            uint amount = 0;
            uint ustart = startHeight / decrementInterval;
            if (ustart < generationAmountLength)
            {
                uint istart = startHeight % decrementInterval;
                uint uend = endHeight / decrementInterval;
                uint iend = endHeight % decrementInterval;
                if (uend >= generationAmountLength)
                {
                    uend = (uint)generationAmountLength;
                    iend = 0;
                }
                if (iend == 0)
                {
                    uend--;
                    iend = decrementInterval;
                }
                while (ustart < uend)
                {
                    amount += (decrementInterval - istart) * Blockchain.GenerationAmount[ustart];
                    ustart++;
                    istart = 0;
                }
                amount += (iend - istart) * Blockchain.GenerationAmount[ustart];
            }

            Fixed8 fractionalShare = value / 100000000;
            var generated = fractionalShare * amount;
            var sysFee = fractionalShare * (GetSysFeeAmountForHeight(snapshot, endHeight - 1) -
                     (startHeight == 0 ? 0 : GetSysFeeAmountForHeight(snapshot, startHeight - 1)));

            return (generated, sysFee);
        }

        public static ContractTransaction? MakeTransferTransaction(Snapshot snapshot,
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
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Witnesses = Array.Empty<Witness>(),
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
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
            };
        }

        public static ClaimTransaction? MakeClaimTransaction(Snapshot snapshot, UInt160 address, UInt256 assetId)
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
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Inputs = Array.Empty<CoinReference>(),
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = Blockchain.UtilityToken.Hash,
                            Value = snapshot.CalculateBonus(coinReferences),
                            ScriptHash = address
                        }
                    },
                    Witnesses = Array.Empty<Witness>()
                };
            }

            return null;
        }

        public static (InvocationTransaction?, ApplicationEngine) MakeDeploymentTransaction(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, Newtonsoft.Json.Linq.JToken contract)
        {
            var tx = BuildInvocationTx(() => BuildContractCreateScript(contract));
            tx.Version = 1;
            var engine = ApplicationEngine.Run(tx.Script, tx, null, true);
            if ((engine.State & VMState.FAULT) != 0)
            {
                throw new Exception("NeoVM Faulted");
            }

            var gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            tx.Gas = (gas < Fixed8.Zero) ? Fixed8.Zero : gas.Ceiling();

            return (AddTransactionFee(snapshot, addresses, tx), engine);
        }

        public static (InvocationTransaction?, ApplicationEngine) MakeInvocationTransaction(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, UInt160 scriptHash, ContractParameter[] parameters, IEnumerable<CoinReference>? inputs = null, IEnumerable<TransactionOutput>? outputs = null)
        {
            var tx = BuildInvocationTx(() => BuildInvocationScript(scriptHash, parameters), inputs, outputs);
            var engine = ApplicationEngine.Run(tx.Script, tx);
            if ((engine.State & VMState.FAULT) != 0)
            {
                throw new Exception("NeoVM Faulted");
            }

            return addresses.IsEmpty ? (null, engine) : (AddTransactionFee(snapshot, addresses, tx), engine);
        }

        private static InvocationTransaction? AddTransactionFee(Snapshot snapshot, ImmutableHashSet<UInt160> addresses, InvocationTransaction tx)
        {
            var fee = Fixed8.Zero;
            if (tx.Size > 1024)
            {
                fee = Fixed8.FromDecimal(0.001m);
                fee += Fixed8.FromDecimal(tx.Size * 0.00001m);
            }
            fee += tx.SystemFee;

            if (fee == Fixed8.Zero)
            {
                return tx;
            }

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

        private static InvocationTransaction BuildInvocationTx(Func<ScriptBuilder> func, IEnumerable<CoinReference>? inputs = null, IEnumerable<TransactionOutput>? outputs = null)
        {
            using (var builder = func())
            {
                return new InvocationTransaction
                {
                    Script = builder.ToArray(),
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Inputs = inputs?.ToArray() ?? Array.Empty<CoinReference>(),
                    Outputs = outputs?.ToArray() ?? Array.Empty<TransactionOutput>(),
                    Witnesses = Array.Empty<Witness>(),
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
            static ContractParameterType TypeParse(JToken jtoken) => Enum.Parse<ContractParameterType>(jtoken.Value<string>());

            var contractData = json.Value<string>("contract-data").HexToBytes();

            var entryPoint = json.Value<string>("entry-point");
            var entryFunction = json["functions"].Single(t => t.Value<string>("name") == entryPoint);
            var entryParameters = entryFunction["parameters"].Select(t => TypeParse(t.Value<string>("type")));
            var entryReturnType = TypeParse(entryFunction.Value<string>("return-type"));

            var qqq = entryFunction.Value<string>("return-type");

            var props = json["properties"];
            var title = props?.Value<string>("title") ?? json.Value<string>("name");
            var description = props?.Value<string>("description") ?? "no description provided";
            var version = props?.Value<string>("version") ?? "0.1.0";
            var author = props?.Value<string>("author") ?? "no description provided";
            var email = props?.Value<string>("email") ?? "nobody@fake.email";

            var contractPropertyState = ContractPropertyState.NoProperty;
            if (props?.Value<bool?>("has-storage") ?? false) contractPropertyState |= ContractPropertyState.HasStorage;
            if (props?.Value<bool?>("has-dynamic-invoke") ?? false) contractPropertyState |= ContractPropertyState.HasDynamicInvoke;
            if (props?.Value<bool?>("is-payable") ?? false) contractPropertyState |= ContractPropertyState.Payable;

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
