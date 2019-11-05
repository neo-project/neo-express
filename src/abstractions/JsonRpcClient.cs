using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NeoExpress.Abstractions
{
    public static class JsonRpcClient
    {
        public static async Task<JToken?> RpcCall(Uri uri, string methodName, JArray paramList)
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
                var j = await JToken.ReadFromAsync(reader).ConfigureAwait(false);

                JToken? error = j["error"];
                if (error != null)
                {
                    var code = error.Value<int>("code");
                    var message = error.Value<string>("message");

                    throw new JsonRpcException(message, code);
                }

                JToken? result = j["result"];
                if (result == null)
                {
                    throw new Exception("Invalid JSON RPC Response");
                }

                return result;
            }
        }

    }
}
