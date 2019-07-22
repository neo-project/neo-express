using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
    }
}
