using System;
using System.Collections.Generic;
using System.Linq;
using Neo.IO.Json;

namespace NeoExpress
{
    public class ExpressRpcFoundStates
    {
        public bool Truncated;
        public (string key, string value)[] Results;
        public byte[]? FirstProof;
        public byte[]? LastProof;

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

        static byte[] ProofFromJson(JObject json)
            => json == null ? null : Convert.FromBase64String(json.AsString());
    }
}