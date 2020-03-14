// using NeoExpress.Models;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Linq;
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net.Http;
// using System.Text;
// using System.Threading.Tasks;

// namespace NeoExpress
// {
//     class JsonRpcException : Exception
//     {
//         public int Code { get; }

//         public JsonRpcException(int code, string? message) : base(message)
//         {
//             Code = code;
//         }
//     }

//     internal static class NeoRpcClient
//     {
//         private static async Task<JToken?> RpcCall(Uri uri, string methodName, JArray paramList)
//         {
//             var request = new JObject
//             {
//                 ["id"] = 1,
//                 ["jsonrpc"] = "2.0",
//                 ["method"] = methodName,
//                 ["params"] = paramList
//             };

//             var client = new HttpClient();
//             var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
//             var response = await client.PostAsync(uri, content);
//             response.EnsureSuccessStatusCode();

//             var stream = await response.Content.ReadAsStreamAsync();
//             using (var streamReader = new StreamReader(stream))
//             using (var reader = new JsonTextReader(streamReader))
//             {
//                 var j = await JToken.ReadFromAsync(reader).ConfigureAwait(false);

//                 JToken? error = j["error"];
//                 if (error != null)
//                 {
//                     var code = error.Value<int>("code");
//                     var message = error.Value<string>("message");

//                     throw new JsonRpcException(code, message);
//                 }

//                 JToken? result = j["result"];
//                 if (result == null)
//                 {
//                     throw new Exception("Invalid JSON RPC Response");
//                 }

//                 return result;
//             }
//         }

//         public static Task<JToken?> ExpressClaim(Uri uri, string asset, string address)
//         {
//             return RpcCall(uri, "express-claim", new JArray(asset, address));
//         }

//         public static Task<JToken?> ExpressCreateCheckpoint(Uri uri, string checkpointPath)
//         {
//             return RpcCall(uri, "express-create-checkpoint", new JArray(checkpointPath));
//         }

//         public static Task<JToken?> ExpressDeployContract(Uri uri, ExpressContract contract, string address)
//         {
//             var serializer = new JsonSerializer();
//             using var writer = new JTokenWriter();
//             serializer.Serialize(writer, contract);
//             if (writer.Token == null)
//                 throw new ApplicationException($"Could not serialize {nameof(ExpressContract)} for deployment");

//             return RpcCall(uri, "express-deploy-contract", new JArray(writer.Token, address));
//         }

//         public static Task<JToken?> ExpressGetContractStorage(Uri uri, string scriptHash)
//         {
//             return RpcCall(uri, "express-get-contract-storage", new JArray(scriptHash));
//         }

//         public static Task<JToken?> ExpressInvokeContract(Uri uri, string scriptHash, IEnumerable<JObject> @params, string? address = null)
//         {
//             return RpcCall(uri, "express-invoke-contract", new JArray(scriptHash, new JArray(@params), address == null ? JValue.CreateNull() : JValue.CreateString(address)));
//         }

//         public static Task<JToken?> ExpressShowCoins(Uri uri, string address)
//         {
//             return RpcCall(uri, "express-show-coins", new JArray(address));
//         }

//         public static Task<JToken?> ExpressSubmitSignatures(Uri uri, JToken? context, JToken signatures)
//         {
//             if (context == null)
//             {
//                 throw new ArgumentException(nameof(context));
//             }

//             return RpcCall(uri, "express-submit-signatures", new JArray(context, signatures));
//         }

//         public static Task<JToken?> ExpressTransfer(Uri uri, string asset, string quantity, string senderAddress, string receiverAddress)
//         {
//             return RpcCall(uri, "express-transfer", new JArray(asset, quantity, senderAddress, receiverAddress));
//         }

//         public static Task<JToken?> GetAccountState(Uri uri, string address)
//         {
//             return RpcCall(uri, "getaccountstate", new JArray(address));
//         }

//         public static Task<JToken?> GetContractState(Uri uri, string scriptHash)
//         {
//             return RpcCall(uri, "getcontractstate", new JArray(scriptHash.ToString()));
//         }

//         public static Task<JToken?> GetApplicationLog(Uri uri, string txid)
//         {
//             return RpcCall(uri, "getapplicationlog", new JArray(txid));
//         }

//         public static Task<JToken?> GetUnclaimed(Uri uri, string address)
//         {
//             return RpcCall(uri, "getunclaimed", new JArray(address));
//         }

//         public static Task<JToken?> GetClaimable(Uri uri, string address)
//         {
//             return RpcCall(uri, "getclaimable", new JArray(address));
//         }

//         public static Task<JToken?> GetUnspents(Uri uri, string address)
//         {
//             return RpcCall(uri, "getunspents", new JArray(address));
//         }

//         public static Task<JToken?> SendRawTransaction(Uri uri, Neo.Network.P2P.Payloads.Transaction tx)
//         {
//             var txData = Neo.Helper.ToHexString(Neo.IO.Helper.ToArray(tx));
//             return RpcCall(uri, "sendrawtransaction", new JArray(txData));
//         }

//         public static Task<JToken?> GetRawTransaction(Uri uri, string txid, bool verbose = true)
//         {
//             return RpcCall(uri, "getrawtransaction", new JArray(txid, verbose ? 1 : 0));            
//         }
//     }
// }
