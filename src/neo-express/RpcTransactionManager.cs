using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;

namespace NeoExpress
{
    static class RpcTransactionManager
    {
        private static bool IsMultiSigContract(ExpressWalletAccount account) =>
            Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script.ToByteArray());

        static byte[] Sign(byte[] hashData, DevWalletAccount account)
        {
            var key = account.GetKey();
            if (key == null)
            {
                throw new Exception("DevWalletAccount missing key");
            }

            var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
            return Neo.Cryptography.Crypto.Default.Sign(hashData, key.PrivateKey, publicKey);
        }


        public static Witness GetWitness(Transaction tx, ExpressChain chain, ExpressWalletAccount account)
        {
            var txHashData = Neo.Network.P2P.Helper.GetHashData(tx);
            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

            if (IsMultiSigContract(account))
            {
                // neo-express only uses multi sig contracts for consensus nodes
                var verification = devAccount.Contract.Script;
                var signatureCount = devAccount.Contract.ParameterList.Length;

                var signatures = chain.ConsensusNodes
                    .Take(signatureCount)
                    .Select(node =>
                    {
                        var account = DevWalletAccount.FromExpressWalletAccount(node.Wallet.DefaultAccount);
                        return Sign(txHashData, account);
                    })
                    .ToList();

                var invocation = new byte[signatures.Aggregate(0, (a, sig) => a + sig.Length + 1)];

                var curPos = 0;
                for (int i = 0; i < signatures.Count; i++)
                {
                    invocation[curPos] = 0x40;
                    signatures[i].AsSpan().CopyTo(invocation.AsSpan().Slice(curPos + 1, signatures[i].Length));
                    curPos += 1 + signatures[i].Length;
                }

                return new Witness()
                {
                    InvocationScript = invocation,
                    VerificationScript = verification,
                };
            }
            else
            {
                static byte[] CreateSignatureRedeemScript(DevWalletAccount account)
                {
                    var key = account.GetKey();
                    if (key == null)
                    {
                        throw new Exception("DevWalletAccount missing key");
                    }

                    using var sb = new Neo.VM.ScriptBuilder();
                    sb.EmitPush(key.PublicKey.EncodePoint(true));
                    sb.Emit(Neo.VM.OpCode.CHECKSIG);
                    return sb.ToArray();
                }

                var signature = Sign(txHashData, devAccount);

                var invocation = new byte[signature.Length + 1];
                invocation[0] = 0x40;
                signature.AsSpan().CopyTo(invocation.AsSpan().Slice(1, signature.Length));
                var verification = CreateSignatureRedeemScript(devAccount);
                return new Witness()
                {
                    InvocationScript = invocation,
                    VerificationScript = verification,
                };
            }
        }

        public static ContractTransaction CreateContractTransaction(UInt256 assetId, string quantity, UnspentTransaction[] transactions, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            static CoinReference ConvertUnspentTransaction(UnspentTransaction t) => new CoinReference()
            {
                PrevHash = UInt256.Parse(t.TransactionId),
                PrevIndex = t.Index
            };

            IEnumerable<UnspentTransaction> GetInputs(decimal amount)
            {
                foreach (var tx in transactions.OrderByDescending(t => t.Value))
                {
                    if (amount < tx.Value)
                    {
                        yield return tx;
                    }
                    else
                    {
                        yield return tx;
                    }

                    amount -= tx.Value;
                    if (amount <= decimal.Zero)
                    {
                        break;
                    }
                }
            }

            var sum = transactions.Sum(t => t.Value);

            if (quantity.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return new ContractTransaction()
                {
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Inputs = transactions.Select(ConvertUnspentTransaction).ToArray(),
                    Outputs = new TransactionOutput[] {
                        new TransactionOutput
                        {
                            AssetId = assetId,
                            Value = Neo.Fixed8.FromDecimal(sum),
                            ScriptHash = Neo.Wallets.Helper.ToScriptHash(receiver.ScriptHash)
                        }
                    }
                };
            }

            if (decimal.TryParse(quantity, out var result))
            {
                if (sum < result)
                    throw new ApplicationException("Insufficient funds for transfer");

                var inputs = GetInputs(result);
                sum = inputs.Sum(t => t.Value);

                return new ContractTransaction()
                {
                    Attributes = Array.Empty<TransactionAttribute>(),
                    Inputs = inputs.Select(ConvertUnspentTransaction).ToArray(),
                    Outputs = new TransactionOutput[] {
                        new TransactionOutput
                        {
                            AssetId = assetId,
                            Value = Neo.Fixed8.FromDecimal(sum - result),
                            ScriptHash = Neo.Wallets.Helper.ToScriptHash(sender.ScriptHash)
                        },
                        new TransactionOutput
                        {
                            AssetId = assetId,
                            Value = Neo.Fixed8.FromDecimal(result),
                            ScriptHash = Neo.Wallets.Helper.ToScriptHash(receiver.ScriptHash)
                        },
                    }
                };
            }

            throw new ArgumentException(nameof(quantity));
        }
    }
}
