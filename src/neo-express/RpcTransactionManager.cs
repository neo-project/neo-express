// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Neo;
// using Neo.Ledger;
// using Neo.Network.P2P.Payloads;
// using Neo.SmartContract;
// using Neo.VM;
// using NeoExpress.Models;
// using Newtonsoft.Json.Linq;

// namespace NeoExpress
// {
//     static class RpcTransactionManager
//     {
//         private static bool IsMultiSigContract(ExpressWalletAccount account) =>
//             Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script.ToByteArray());

//         static byte[] Sign(byte[] hashData, DevWalletAccount account)
//         {
//             var key = account.GetKey();
//             if (key == null)
//             {
//                 throw new Exception("DevWalletAccount missing key");
//             }

//             var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
//             return Neo.Cryptography.Crypto.Default.Sign(hashData, key.PrivateKey, publicKey);
//         }


//         public static Witness GetWitness(Transaction tx, ExpressChain chain, ExpressWalletAccount account)
//         {
//             var txHashData = Neo.Network.P2P.Helper.GetHashData(tx);
//             var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

//             if (IsMultiSigContract(account))
//             {
//                 // neo-express only uses multi sig contracts for consensus nodes
//                 var verification = devAccount.Contract.Script;
//                 var signatureCount = devAccount.Contract.ParameterList.Length;

//                 var signatures = chain.ConsensusNodes
//                     .Take(signatureCount)
//                     .Select(node =>
//                     {
//                         var account = DevWalletAccount.FromExpressWalletAccount(node.Wallet.DefaultAccount);
//                         return Sign(txHashData, account);
//                     })
//                     .ToList();

//                 var invocation = new byte[signatures.Aggregate(0, (a, sig) => a + sig.Length + 1)];

//                 var curPos = 0;
//                 for (int i = 0; i < signatures.Count; i++)
//                 {
//                     invocation[curPos] = 0x40;
//                     signatures[i].AsSpan().CopyTo(invocation.AsSpan().Slice(curPos + 1, signatures[i].Length));
//                     curPos += 1 + signatures[i].Length;
//                 }

//                 return new Witness()
//                 {
//                     InvocationScript = invocation,
//                     VerificationScript = verification,
//                 };
//             }
//             else
//             {
//                 static byte[] CreateSignatureRedeemScript(DevWalletAccount account)
//                 {
//                     var key = account.GetKey();
//                     if (key == null)
//                     {
//                         throw new Exception("DevWalletAccount missing key");
//                     }

//                     using var sb = new Neo.VM.ScriptBuilder();
//                     sb.EmitPush(key.PublicKey.EncodePoint(true));
//                     sb.Emit(Neo.VM.OpCode.CHECKSIG);
//                     return sb.ToArray();
//                 }

//                 var signature = Sign(txHashData, devAccount);

//                 var invocation = new byte[signature.Length + 1];
//                 invocation[0] = 0x40;
//                 signature.AsSpan().CopyTo(invocation.AsSpan().Slice(1, signature.Length));
//                 var verification = CreateSignatureRedeemScript(devAccount);
//                 return new Witness()
//                 {
//                     InvocationScript = invocation,
//                     VerificationScript = verification,
//                 };
//             }
//         }

//         public static ClaimTransaction CreateClaimTransaction(ExpressWalletAccount account, ClaimableResponse claimable, UInt256 assetId)
//         {
//             const int MAX_CLAIMS_AMOUNT = 50;

//             var claims = claimable.Transactions
//                 .Take(MAX_CLAIMS_AMOUNT)
//                 .Select(_tx => new CoinReference()
//                 {
//                     PrevHash = Neo.UInt256.Parse(_tx.TransactionId),
//                     PrevIndex = _tx.Index,
//                 });

//             return new ClaimTransaction()
//             {
//                 Attributes = Array.Empty<TransactionAttribute>(),
//                 Inputs = Array.Empty<CoinReference>(),
//                 Outputs = new[]
//                 {
//                     new TransactionOutput
//                     {
//                         AssetId = assetId,
//                         Value = Neo.Fixed8.FromDecimal(claimable.Unclaimed),
//                         ScriptHash = Neo.Wallets.Helper.ToScriptHash(account.ScriptHash)
//                     }
//                 },
//                 Claims = claims.ToArray(),
//             };
//         }

//         private static IEnumerable<(UnspentTransaction tx, decimal amount)> GetInputTransactions(IEnumerable<UnspentTransaction> transactions, decimal amount)
//         {
//             foreach (var tx in transactions.OrderByDescending(t => t.Value))
//             {
//                 if (amount < tx.Value)
//                 {
//                     yield return (tx, amount);
//                 }
//                 else
//                 {
//                     yield return (tx, tx.Value);
//                 }

//                 amount -= tx.Value;
//                 if (amount <= decimal.Zero)
//                 {
//                     break;
//                 }
//             }
//         }

//         private static CoinReference ConvertInput((UnspentTransaction tx, decimal amount) inputTx)
//         {
//             return new CoinReference()
//             {
//                 PrevHash = UInt256.Parse(inputTx.tx.TransactionId),
//                 PrevIndex = inputTx.tx.Index
//             };
//         }

//         static UnspentTransaction[] GetUnspentAssets(UInt256 assetId, UnspentsResponse unspents)
//         {
//             for (int i = 0; i < unspents.Balance.Length; i++)
//             {
//                 var balance = unspents.Balance[i];
//                 if (UInt256.TryParse(balance.AssetHash, out var balanceAsset)
//                     && balanceAsset == assetId)
//                 {
//                     return balance.Transactions;
//                 }
//             }

//             return Array.Empty<UnspentTransaction>();
//         }

//         public static ContractTransaction CreateContractTransaction(UInt256 assetId, string quantity, UnspentsResponse unspents, ExpressWalletAccount sender, ExpressWalletAccount receiver)
//         {
//             var assets = GetUnspentAssets(assetId, unspents);
//             var sum = assets.Sum(t => t.Value);

//             if (quantity.Equals("all", StringComparison.OrdinalIgnoreCase))
//             {
//                 return new ContractTransaction()
//                 {
//                     Attributes = Array.Empty<TransactionAttribute>(),
//                     Inputs = assets.Select(t => new CoinReference
//                         {
//                             PrevHash= UInt256.Parse(t.TransactionId),
//                             PrevIndex = t.Index
//                         }).ToArray(),
//                     Outputs = new TransactionOutput[] {
//                         new TransactionOutput
//                         {
//                             AssetId = assetId,
//                             Value = Fixed8.FromDecimal(sum),
//                             ScriptHash = Neo.Wallets.Helper.ToScriptHash(receiver.ScriptHash)
//                         }
//                     }
//                 };
//             }

//             if (decimal.TryParse(quantity, out var result))
//             {
//                 if (sum < result)
//                     throw new ApplicationException("Insufficient funds for transfer");

//                 var inputs = GetInputTransactions(assets, result);
//                 sum = inputs.Sum(t => t.tx.Value);

//                 return new ContractTransaction()
//                 {
//                     Attributes = Array.Empty<TransactionAttribute>(),
//                     Inputs = inputs.Select(t => new CoinReference
//                         {
//                             PrevHash = UInt256.Parse(t.tx.TransactionId),
//                             PrevIndex = t.tx.Index
//                         }).ToArray(),
//                     Outputs = new TransactionOutput[] {
//                         new TransactionOutput
//                         {
//                             AssetId = assetId,
//                             Value = Neo.Fixed8.FromDecimal(sum - result),
//                             ScriptHash = Neo.Wallets.Helper.ToScriptHash(sender.ScriptHash)
//                         },
//                         new TransactionOutput
//                         {
//                             AssetId = assetId,
//                             Value = Neo.Fixed8.FromDecimal(result),
//                             ScriptHash = Neo.Wallets.Helper.ToScriptHash(receiver.ScriptHash)
//                         },
//                     }
//                 };
//             }

//             throw new ArgumentException(nameof(quantity));
//         }

//         public static InvocationTransaction CreateDeploymentTransaction(ExpressContract contract, ExpressWalletAccount sender, UnspentsResponse unspents)
//         {
//             var gasAssetId = Neo.Ledger.Blockchain.UtilityToken.Hash;
            
//             using var builder = BuildContractCreateScript(contract);
//             var contractPropertyState = GetContractPropertyState(contract);

//             decimal fee = 100 - 10; // first 10 GAS is free

//             if (contractPropertyState.HasFlag(ContractPropertyState.HasStorage))
//             {
//                 fee += 400;
//             }

//             if (contractPropertyState.HasFlag(ContractPropertyState.HasDynamicInvoke))
//             {
//                 fee += 500;
//             }

//             var assets = GetUnspentAssets(gasAssetId, unspents);
//             var sum = assets.Sum(t => t.Value);

//             if (sum < fee)
//                 throw new ApplicationException("Insufficient funds for deployment");

//             var inputs = GetInputTransactions(assets, fee);
//             sum = inputs.Sum(t => t.tx.Value);

//             return new InvocationTransaction
//             {
//                 Version = 1,
//                 Attributes = Array.Empty<TransactionAttribute>(),
//                 Inputs = inputs.Select(t => new CoinReference
//                     {
//                         PrevHash = UInt256.Parse(t.tx.TransactionId),
//                         PrevIndex = t.tx.Index
//                     }).ToArray(),
//                 Outputs = new TransactionOutput[] {
//                     new TransactionOutput
//                     {
//                         AssetId = gasAssetId,
//                         Value = Neo.Fixed8.FromDecimal(sum - fee),
//                         ScriptHash = Neo.Wallets.Helper.ToScriptHash(sender.ScriptHash)
//                     },
//                 },
//                 Script = builder.ToArray(),
//                 Gas = Fixed8.FromDecimal(fee),
//             };
//         }

//         static ContractPropertyState GetContractPropertyState(ExpressContract contract)
//         {
//             bool GetBoolValue(string keyName)
//             {
//                 if (contract.Properties.TryGetValue(keyName, out var value))
//                 {
//                     return bool.Parse(value);
//                 }

//                 return false;
//             }

//             var contractPropertyState = ContractPropertyState.NoProperty;
//             if (GetBoolValue("has-storage")) contractPropertyState |= ContractPropertyState.HasStorage;
//             if (GetBoolValue("has-dynamic-invoke")) contractPropertyState |= ContractPropertyState.HasDynamicInvoke;
//             if (GetBoolValue("is-payable")) contractPropertyState |= ContractPropertyState.Payable;

//             return contractPropertyState;
//         }

//         private static ScriptBuilder BuildContractCreateScript(ExpressContract contract)
//         {
//             var contractData = contract.ContractData.HexToBytes();

//             var entryFunction =contract.Functions.Single(f => f.Name == contract.EntryPoint);
           
//             var entryParameters = entryFunction.Parameters.Select(p => Enum.Parse<ContractParameterType>(p.Type));
//             var entryReturnType = Enum.Parse<ContractParameterType>(entryFunction.ReturnType); 

//             var title = contract.Properties.GetValueOrDefault("title", contract.Name);  
//             var description = contract.Properties.GetValueOrDefault("description", "no description provided");
//             var version = contract.Properties.GetValueOrDefault("version", "0.1.0");
//             var author = contract.Properties.GetValueOrDefault("author", "no description provided");
//             var email = contract.Properties.GetValueOrDefault("email", "nobody@fake.email");
//             var contractPropertyState = GetContractPropertyState(contract);

//             var builder = new ScriptBuilder();
//             builder.EmitSysCall("Neo.Contract.Create",
//                 contractData,
//                 entryParameters.ToArray(),
//                 entryReturnType,
//                 contractPropertyState,
//                 title,
//                 version,
//                 author,
//                 email,
//                 description);
//             return builder;
//         }
//     }
// }
