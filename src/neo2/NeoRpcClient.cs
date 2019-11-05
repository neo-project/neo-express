using NeoExpress.Abstractions;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neo2Express
{
    internal static class NeoRpcClient
    {
        public static Task<JToken?> ExpressClaim(Uri uri, string asset, string address)
        {
            return JsonRpcClient.RpcCall(uri, "express-claim", new JArray(asset, address));
        }

        public static Task<JToken?> ExpressCreateCheckpoint(Uri uri, string checkpointPath)
        {
            return JsonRpcClient.RpcCall(uri, "express-create-checkpoint", new JArray(checkpointPath));
        }

        public static Task<JToken?> ExpressDeployContract(Uri uri, ExpressContract contract, string address)
        {
            JToken SerializeContract()
            {
                var serializer = new JsonSerializer();
                using (var writer = new JTokenWriter())
                {
                    serializer.Serialize(writer, contract);
                    return writer.Token;
                }
            }
            return JsonRpcClient.RpcCall(uri, "express-deploy-contract", new JArray(SerializeContract(), address));
        }

        public static Task<JToken?> ExpressGetContractStorage(Uri uri, string scriptHash)
        {
            return JsonRpcClient.RpcCall(uri, "express-get-contract-storage", new JArray(scriptHash.ToString()));
        }

        public static Task<JToken?> ExpressInvokeContract(Uri uri, string scriptHash, IEnumerable<JObject> @params, string? address = null)
        {
            return JsonRpcClient.RpcCall(uri, "express-invoke-contract", new JArray(scriptHash, new JArray(@params), address));
        }

        public static Task<JToken?> ExpressShowCoins(Uri uri, string address)
        {
            return JsonRpcClient.RpcCall(uri, "express-show-coins", new JArray(address));
        }

        public static Task<JToken?> ExpressSubmitSignatures(Uri uri, JToken? context, JToken signatures)
        {
            if (context == null)
            {
                throw new ArgumentException(nameof(context));
            }

            return JsonRpcClient.RpcCall(uri, "express-submit-signatures", new JArray(context, signatures));
        }

        public static Task<JToken?> ExpressTransfer(Uri uri, string asset, string quantity, string senderAddress, string receiverAddress)
        {
            return JsonRpcClient.RpcCall(uri, "express-transfer", new JArray(asset, quantity, senderAddress, receiverAddress));
        }

        public static Task<JToken?> GetAccountState(Uri uri, string address)
        {
            return JsonRpcClient.RpcCall(uri, "getaccountstate", new JArray(address));
        }

        public static Task<JToken?> GetContractState(Uri uri, string scriptHash)
        {
            return JsonRpcClient.RpcCall(uri, "getcontractstate", new JArray(scriptHash.ToString()));
        }

        public static Task<JToken?> GetApplicationLog(Uri uri, string txid)
        {
            return JsonRpcClient.RpcCall(uri, "getapplicationlog", new JArray(txid));
        }

        public static Task<JToken?> GetUnclaimed(Uri uri, string address)
        {
            return JsonRpcClient.RpcCall(uri, "getunclaimed", new JArray(address));
        }

        public static Task<JToken?> GetClaimable(Uri uri, string address)
        {
            return JsonRpcClient.RpcCall(uri, "getclaimable", new JArray(address));
        }

        public static Task<JToken?> GetUnspents(Uri uri, string address)
        {
            return JsonRpcClient.RpcCall(uri, "getunspents", new JArray(address));
        }
    }
}
