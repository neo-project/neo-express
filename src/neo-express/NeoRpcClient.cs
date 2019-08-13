using NeoExpress.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NeoExpress
{
    static class NeoRpcClient
    {
        static async Task<JToken> RpcCall(Uri uri, string methodName, JArray paramList)
        {
            var request = new JObject
            {
                ["id"] = 1,
                ["jsonrpc"] = "2.0",
                ["method"] = methodName,
                ["params"] = paramList
            };

            var client = new HttpClient();
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri, content);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var j = await JToken.ReadFromAsync(reader);
                return j["result"];
            }
        }

        public static Task<JToken> ExpressTransfer(Uri uri, string asset, string quantity, string senderAddress, string receiverAddress)
        {
            return RpcCall(uri, "express-transfer", new JArray(asset, quantity, senderAddress, receiverAddress));
        }

        public static Task<JToken> ExpressSubmitSignatures(Uri uri, JToken context, JToken signatures)
        {
            return RpcCall(uri, "express-submit-signatures", new JArray(context, signatures));
        }

        public static Task<JToken> GetAccountState(Uri uri, string address)
        {
            return RpcCall(uri, "getaccountstate", new JArray(address));
        }

        public static Task<JToken> ExpressShowCoins(Uri uri, string address)
        {
            return RpcCall(uri, "express-show-coins", new JArray(address));
        }

        public static Task<JToken> ExpressShowGas(Uri uri, string address)
        {
            return RpcCall(uri, "express-show-gas", new JArray(address));
        }

        public static Task<JToken> ExpressClaim(Uri uri, string asset, string address)
        {
            return RpcCall(uri, "express-claim", new JArray(asset, address));
        }

        private static JToken SerializeToJToken<T>(T obj)
        {
            var serializer = new JsonSerializer();
            using (var writer = new JTokenWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.Token;
            }
        }

        public static Task<JToken> ExpressDeployContract(Uri uri, ExpressContract contract, string address)
        {
            return RpcCall(uri, "express-deploy-contract", new JArray(SerializeToJToken(contract), address));
        }

        //public static Task<JToken> ExpressInvokeContract(Uri uri, string scriptHash, IEnumerable<ContractParameter> scriptParams, string address = null)
        //{
        //    var @params = new JArray(scriptParams.Select(p => JObject.Parse(p.ToJson().ToString())));
        //    return RpcCall(uri, "express-invoke-contract", new JArray(
        //        scriptHash.ToString(),
        //        @params,
        //        address));
        //}

        public static Task<JToken> GetContractState(Uri uri, string scriptHash)
        {
            return RpcCall(uri, "getcontractstate", new JArray(scriptHash.ToString()));
        }

        public static Task<JToken> ExpressGetContractStorage(Uri uri, string scriptHash)
        {
            return RpcCall(uri, "express-get-contract-storage", new JArray(scriptHash.ToString()));
        }

    }
}
