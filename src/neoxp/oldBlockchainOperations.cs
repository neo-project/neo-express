// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net;
// using System.Numerics;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;
// using Neo;
// using Neo.BlockchainToolkit;
// using Neo.BlockchainToolkit.Persistence;
// using Neo.Cryptography.ECC;
// using Neo.IO;
// using Neo.Network.P2P.Payloads;
// using Neo.Network.RPC;
// using Neo.Network.RPC.Models;
// using Neo.SmartContract;
// using Neo.SmartContract.Manifest;
// using Neo.SmartContract.Native;
// using Neo.VM;
// using NeoExpress.Models;
// using NeoExpress.Node;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Linq;
// using Nito.Disposables;

// namespace NeoExpress
// {
//     using StackItemType = Neo.VM.Types.StackItemType;

//     public class BlockchainOperations
//     {

//         // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/transaction.md
//         public async Task<UInt256> TransferAsync(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             var assetHash = await ParseAssetAsync(expressNode, asset);
//             var senderHash = sender.GetScriptHashAsUInt160();
//             var receiverHash = receiver.GetScriptHashAsUInt160();

//             if ("all".Equals(quantity, StringComparison.OrdinalIgnoreCase))
//             {
//                 using var sb = new ScriptBuilder();
//                 // balanceOf operation places current balance on eval stack
//                 sb.EmitAppCall(assetHash, "balanceOf", senderHash);
//                 // transfer operation takes 4 arguments, amount is 3rd parameter
//                 // push null onto the stack and then switch positions of the top
//                 // two items on eval stack so null is 4th arg and balance is 3rd
//                 sb.Emit(OpCode.PUSHNULL);
//                 sb.Emit(OpCode.SWAP);
//                 sb.EmitPush(receiverHash);
//                 sb.EmitPush(senderHash);
//                 sb.EmitPush(4);
//                 sb.Emit(OpCode.PACK);
//                 sb.EmitPush("transfer");
//                 sb.EmitPush(assetHash);
//                 sb.EmitSysCall(ApplicationEngine.System_Contract_Call);
//                 return await expressNode.ExecuteAsync(chain, sender, sb.ToArray()).ConfigureAwait(false);
//             }
//             else if (decimal.TryParse(quantity, out var amount))
//             {
//                 var results = await expressNode.InvokeAsync(assetHash.MakeScript("decimals")).ConfigureAwait(false);
//                 if (results.Stack.Length > 0 && results.Stack[0].Type == StackItemType.Integer)
//                 {
//                     var decimals = (byte)(results.Stack[0].GetInteger());
//                     var value = amount.ToBigInteger(decimals);
//                     return await expressNode.ExecuteAsync(chain, sender, assetHash.MakeScript("transfer", senderHash, receiverHash, value, null)).ConfigureAwait(false);
//                 }
//                 else
//                 {
//                     throw new Exception("Invalid response from decimals operation");
//                 }
//             }
//             else
//             {
//                 throw new Exception($"Invalid quantity value {quantity}");
//             }
//         }

//         static async Task<UInt160> ParseAssetAsync(IExpressNode expressNode, string asset)
//         {
//             if ("neo".Equals(asset, StringComparison.OrdinalIgnoreCase))
//             {
//                 return NativeContract.NEO.Hash;
//             }

//             if ("gas".Equals(asset, StringComparison.OrdinalIgnoreCase))
//             {
//                 return NativeContract.GAS.Hash;
//             }

//             if (UInt160.TryParse(asset, out var uint160))
//             {
//                 return uint160;
//             }

//             var contracts = await expressNode.ListNep17ContractsAsync().ConfigureAwait(false);
//             for (int i = 0; i < contracts.Count; i++)
//             {
//                 if (contracts[i].Symbol.Equals(asset, StringComparison.OrdinalIgnoreCase))
//                 {
//                     return contracts[i].ScriptHash;
//                 }
//             }

//             throw new ArgumentException($"Unknown Asset \"{asset}\"", nameof(asset));
//         }

//         // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/contract.md
//         // https://github.com/ProDog/NEO-Test/blob/master/RpcClientTest/Test_ContractClient.cs#L38
//         public async Task<UInt256> DeployContractAsync(ExpressChain chain, string contract, ExpressWalletAccount account)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             var accountHash = account.GetScriptHashAsUInt160();
//             var (nefFile, manifest) = await LoadContract(contract).ConfigureAwait(false);

//             // check for bad opcodes (logic borrowed from neo-cli LoadDeploymentScript)
//             Script script = nefFile.Script;
//             for (var i = 0; i < script.Length;)
//             {
//                 var instruction = script.GetInstruction(i);
//                 if (instruction == null)
//                 {
//                     throw new FormatException($"null opcode found at {i}");
//                 }
//                 else
//                 {
//                     if (!Enum.IsDefined(typeof(OpCode), instruction.OpCode))
//                     {
//                         throw new FormatException($"Invalid opcode found at {i}-{((byte)instruction.OpCode).ToString("x2")}");
//                     }

//                     i += instruction.Size;
//                 }
//             }

//             using var sb = new ScriptBuilder();
//             sb.EmitAppCall(NativeContract.Management.Hash, "deploy", nefFile.ToArray(), manifest.ToJson().ToString());
//             return await expressNode.ExecuteAsync(chain, account, sb.ToArray()).ConfigureAwait(false);

//             static async Task<(NefFile nefFile, ContractManifest manifest)> LoadContract(string contractPath)
//             {
//                 var nefTask = Task.Run(() =>
//                 {
//                     using var stream = File.OpenRead(contractPath);
//                     using var reader = new BinaryReader(stream, Encoding.UTF8, false);
//                     return reader.ReadSerializable<NefFile>();
//                 });

//                 var manifestTask = File.ReadAllBytesAsync(Path.ChangeExtension(contractPath, ".manifest.json"))
//                     .ContinueWith(t => ContractManifest.Parse(t.Result), default, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

//                 await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
//                 return (await nefTask, await manifestTask);
//             }
//         }

//         public async Task<UInt256> InvokeContractAsync(ExpressChain chain, string invocationFilePath, ExpressWalletAccount account, bool trace, decimal additionalGas = 0m)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode(trace);
//             var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
//             var parser = GetContractParameterParser(chain, contracts);
//             var script = await parser.LoadInvocationScriptAsync(invocationFilePath).ConfigureAwait(false);
//             return await expressNode.ExecuteAsync(chain, account, script, additionalGas).ConfigureAwait(false);
//         }

//         public async Task<InvokeResult> TestInvokeContractAsync(ExpressChain chain, string invocationFilePath)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
//             var parser = GetContractParameterParser(chain, contracts);
//             var script = await parser.LoadInvocationScriptAsync(invocationFilePath).ConfigureAwait(false);
//             return await expressNode.InvokeAsync(script).ConfigureAwait(false);
//         }

//         ContractParameterParser GetContractParameterParser(ExpressChain chain, IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts)
//         {
//             ContractParameterParser.TryGetUInt160 tryGetAccount = (string name, out UInt160 scriptHash) =>
//             {
//                 // var account = GetAccount(chain, name);
//                 // if (account != null)
//                 // {
//                 //     scriptHash = account.GetScriptHashAsUInt160();
//                 //     return true;
//                 // }

//                 scriptHash = null!;
//                 return false;
//             };

//             var lookup = contracts.ToDictionary(c => c.manifest.Name, c => c.hash);
//             ContractParameterParser.TryGetUInt160 tryGetContract = (string name, out UInt160 scriptHash) =>
//             {
//                 if (lookup.TryGetValue(name, out var value))
//                 {
//                     scriptHash = value;
//                     return true;
//                 }

//                 foreach (var kvp in lookup)
//                 {
//                     if (string.Equals(name, kvp.Key, StringComparison.OrdinalIgnoreCase))
//                     {
//                         scriptHash = kvp.Value;
//                         return true;
//                     }
//                 }

//                 scriptHash = default!;
//                 return false;
//             };

//             return new ContractParameterParser(tryGetAccount, tryGetContract);
//         }

//         // public ExpressWalletAccount? GetAccount(ExpressChain chain, string name)
//         // {
//         //     var wallet = (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
//         //         .SingleOrDefault(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase));
//         //     if (wallet != null)
//         //     {
//         //         return wallet.DefaultAccount;
//         //     }

//         //     var node = chain.ConsensusNodes
//         //         .SingleOrDefault(n => name.Equals(n.Wallet.Name, StringComparison.OrdinalIgnoreCase));
//         //     if (node != null)
//         //     {
//         //         return node.Wallet.DefaultAccount;
//         //     }

//         //     if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
//         //     {
//         //         return chain.ConsensusNodes
//         //             .Select(n => n.Wallet.Accounts.Single(a => a.IsMultiSigContract()))
//         //             .FirstOrDefault();
//         //     }

//         //     return null;
//         // }

//         public async Task<(BigDecimal balance, Nep17Contract contract)> ShowBalanceAsync(ExpressChain chain, ExpressWalletAccount account, string asset)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             var accountHash = account.GetScriptHashAsUInt160();
//             var assetHash = await ParseAssetAsync(expressNode, asset);

//             using var sb = new ScriptBuilder();
//             sb.EmitAppCall(assetHash, "balanceOf", accountHash);
//             sb.EmitAppCall(assetHash, "symbol");
//             sb.EmitAppCall(assetHash, "decimals");

//             var result = await expressNode.InvokeAsync(sb.ToArray()).ConfigureAwait(false);
//             var stack = result.Stack;
//             if (stack.Length >= 3)
//             {
//                 var balance = stack[0].GetInteger();
//                 var name = string.Empty; //Encoding.UTF8.GetString(stack[1].GetSpan());
//                 var symbol = Encoding.UTF8.GetString(stack[1].GetSpan());
//                 var decimals = (byte)(stack[2].GetInteger());

//                 return (new BigDecimal(balance, decimals), new Nep17Contract(name, symbol, decimals, assetHash));
//             }

//             throw new Exception("invalid script results");
//         }

//         public async Task<(RpcNep17Balance balance, Nep17Contract contract)[]> GetBalancesAsync(ExpressChain chain, ExpressWalletAccount account)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.GetBalancesAsync(account.GetScriptHashAsUInt160());
//         }

//         public async Task<(Transaction tx, RpcApplicationLog? appLog)> ShowTransactionAsync(ExpressChain chain, string txHash)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             var hash = UInt256.Parse(txHash);
//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.GetTransactionAsync(hash);
//         }

//         public async Task<Block> ShowBlockAsync(ExpressChain chain, string blockHash)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             if (string.IsNullOrEmpty(blockHash))
//             {
//                 return await expressNode.GetLatestBlockAsync();
//             }

//             if (UInt256.TryParse(blockHash, out var uint256))
//             {
//                 return await expressNode.GetBlockAsync(uint256);
//             }

//             if (uint.TryParse(blockHash, out var index))
//             {
//                 return await expressNode.GetBlockAsync(index);
//             }

//             throw new ArgumentException($"{nameof(blockHash)} must be block index, block hash or empty", nameof(blockHash));
//         }

//         public async Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(ExpressChain chain, string hashOrContract)
//         {
//             var scriptHash = ParseScriptHash(hashOrContract);

//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.GetStoragesAsync(scriptHash);
//         }

//         public async Task<ContractManifest> GetContractAsync(ExpressChain chain, string hashOrContract)
//         {
//             var scriptHash = ParseScriptHash(hashOrContract);

//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.GetContractAsync(scriptHash);
//         }

//         public async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync(ExpressChain chain)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.ListContractsAsync();
//         }

//         static UInt160 ParseScriptHash(string hashOrContract)
//         {
//             if (UInt160.TryParse(hashOrContract, out var hash))
//             {
//                 return hash;
//             }

//             var parser = new ContractParameterParser();
//             if (parser.TryLoadScriptHash(hashOrContract, out var value))
//             {
//                 return value;
//             }

//             throw new ArgumentException(nameof(hashOrContract));
//         }

//         public async Task<UInt256> DesignateOracleRolesAsync(ExpressChain chain, IEnumerable<ExpressWalletAccount> accounts)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             await Task.Delay(0);

//             throw new NotImplementedException();
//             // var genesisAccount = GetAccount(chain, "genesis") ?? throw new Exception();

//             // byte[] script;
//             // {
//             //     using var sb = new ScriptBuilder();
//             //     var role = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
//             //     var oracles = new ContractParameter(ContractParameterType.Array);
//             //     var oraclesList = (List<ContractParameter>)oracles.Value;

//             //     foreach (var account in accounts)
//             //     {
//             //         var key = DevWalletAccount.FromExpressWalletAccount(account).GetKey() ?? throw new Exception();
//             //         oraclesList.Add(new ContractParameter(ContractParameterType.PublicKey) { Value = key.PublicKey });
//             //     }

//             //     sb.EmitAppCall(NativeContract.Designation.Hash, "designateAsRole", role, oracles);
//             //     script = sb.ToArray();
//             // }

//             // using var expressNode = chain.GetExpressNode();
//             // return await expressNode
//             //     .ExecuteAsync(chain, genesisAccount, script)
//             //     .ConfigureAwait(false);

//         }

//         public async Task<ECPoint[]> GetOracleNodesAsync(ExpressChain chain)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await GetOracleNodesAsync(expressNode);
//         }

//         static async Task<ECPoint[]> GetOracleNodesAsync(IExpressNode expressNode)
//         {
//             var lastBlock = await expressNode.GetLatestBlockAsync().ConfigureAwait(false);

//             byte[] script;
//             {
//                 using var sb = new ScriptBuilder();
//                 var role = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
//                 var index = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)lastBlock.Index + 1 };
//                 sb.EmitAppCall(NativeContract.Designation.Hash, "getDesignatedByRole", role, index);
//                 script = sb.ToArray();
//             }

//             var result = await expressNode.InvokeAsync(script).ConfigureAwait(false);

//             if (result.State == Neo.VM.VMState.HALT
//                 && result.Stack.Length >= 1
//                 && result.Stack[0] is Neo.VM.Types.Array array)
//             {
//                 var nodes = new ECPoint[array.Count];
//                 for (var x = 0; x < array.Count; x++)
//                 {
//                     nodes[x] = ECPoint.DecodePoint(array[x].GetSpan(), ECCurve.Secp256r1);
//                 }
//                 return nodes;
//             }

//             return Array.Empty<ECPoint>();
//         }

//         public async Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> GetOracleRequestsAsync(ExpressChain chain)
//         {
//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             return await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);
//         }

//         public async Task<IReadOnlyList<UInt256>> SubmitOracleResponseAsync(ExpressChain chain, string url, OracleResponseCode responseCode, JObject? responseJson, ulong? requestId)
//         {
//             if (responseCode == OracleResponseCode.Success && responseJson == null)
//             {
//                 throw new ArgumentException("responseJson cannot be null when responseCode is Success", nameof(responseJson));
//             }

//             if (!NodeUtility.InitializeProtocolSettings(chain))
//             {
//                 throw new Exception("could not initialize protocol settings");
//             }

//             using var expressNode = chain.GetExpressNode();
//             var oracleNodes = await GetOracleNodesAsync(expressNode);

//             var txHashes = new List<UInt256>();
//             var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);
//             for (var x = 0; x < requests.Count; x++)
//             {
//                 var (id, request) = requests[x];
//                 if (requestId.HasValue && requestId.Value != id) continue;
//                 if (!string.Equals(url, request.Url, StringComparison.OrdinalIgnoreCase)) continue;

//                 var response = new OracleResponse
//                 {
//                     Code = responseCode,
//                     Id = id,
//                     Result = GetResponseData(request.Filter),
//                 };

//                 var txHash = await expressNode.SubmitOracleResponseAsync(chain, response, oracleNodes);
//                 txHashes.Add(txHash);
//             }
//             return txHashes;

//             byte[] GetResponseData(string filter)
//             {
//                 if (responseCode != OracleResponseCode.Success)
//                 {
//                     return Array.Empty<byte>();
//                 }

//                 System.Diagnostics.Debug.Assert(responseJson != null);

//                 var json = string.IsNullOrEmpty(filter)
//                     ? (JContainer)responseJson : new JArray(responseJson.SelectTokens(filter, true));
//                 return Neo.Utility.StrictUTF8.GetBytes(json.ToString());
//             }
//         }
//     }
// }
