using System;
using System.Linq;
using Neo.IO.Json;

namespace NeoExpress.Models
{
    public class EncodedFoundStates
    {
        public bool Truncated;
        public (string key, string value)[] Results = Array.Empty<(string, string)>();
        public string FirstProof = string.Empty;
        public string LastProof = string.Empty;

        public static EncodedFoundStates FromJson(JObject json)
        {
            return new EncodedFoundStates
            {
                Truncated = json["truncated"].AsBoolean(),
                Results = ((JArray)json["results"])
                    .Select(j => (
                        j["key"].AsString(),
                        j["value"].AsString()
                    ))
                    .ToArray(),
                FirstProof = json["firstProof"].AsString(),
                LastProof = json["lastProof"].AsString(),
            };
        }

    }
}