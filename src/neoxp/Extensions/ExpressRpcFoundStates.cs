using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Json;

namespace NeoExpress
{
    public class ExpressRpcFoundStates
    {
        public bool Truncated;
        public (string key, string value)[] Results = new (string key, string value)[0];
        public (bool hasValue, byte[]? value) FirstProof;
        public (bool hasValue, byte[]? value) LastProof;

        public static ExpressRpcFoundStates FromJson(JObject json)
        {
            return new ExpressRpcFoundStates
            {
                Truncated = json["truncated"].AsBoolean(),
                Results = ((JArray)json["results"])
                    .Select(j => (
                        j["key"].AsString(),
                        j["value"].AsString()
                    ))
                    .ToArray(),
                FirstProof = ProofFromJson(json["firstProof"]),
                LastProof = ProofFromJson(json["lastProof"]),
            };
        }

        private static (bool hasValue, byte[]? value) ProofFromJson(JObject json)
            => json == null ? (false, null) : (true, Convert.FromBase64String(json.AsString()));
    }
}