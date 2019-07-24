using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Express
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

        public static Task<JToken> ExpressTransfer(Uri uri, string asset, string quantity, UInt160 senderAddress, UInt160 receiverAddress)
        {
            return RpcCall(uri, "express-transfer", new JArray(asset, quantity, senderAddress.ToAddress(), receiverAddress.ToAddress()));
        }

        public static Task<JToken> ExpressSubmitSignatures(Uri uri, JToken context, JToken signatures)
        {
            return RpcCall(uri, "express-submit-signatures", new JArray(context, signatures));
        }

        public static Task<JToken> GetAccountState(Uri uri, UInt160 address)
        {
            return RpcCall(uri, "getaccountstate", new JArray(address.ToAddress()));
        }

        public static Task<JToken> ExpressShowCoins(Uri uri, UInt160 address)
        {
            return RpcCall(uri, "express-show-coins", new JArray(address.ToAddress()));
        }

        public static Task<JToken> ExpressShowGas(Uri uri, UInt160 address)
        {
            return RpcCall(uri, "express-show-gas", new JArray(address.ToAddress()));
        }

        public static Task<JToken> ExpressClaim(Uri uri, string asset, UInt160 address)
        {
            return RpcCall(uri, "express-claim", new JArray(asset, address.ToAddress()));
        }

        private static JToken ToJToken(Action<JsonWriter> action)
        {
            using (var writer = new JTokenWriter())
            {
                action(writer);
                return writer.Token;
            }
        }

        public static Task<JToken> ExpressDeployContract(Uri uri, DevContract contract, UInt160 hash)
        {
            return RpcCall(uri, "express-deploy-contract", new JArray(
                ToJToken(contract.ToJson), hash.ToAddress()));
        }

        public static Task<JToken> GetContractState(Uri uri, UInt160 scriptHash)
        {
            return RpcCall(uri, "getcontractstate", new JArray(scriptHash.ToString()));
        }
    }
}
