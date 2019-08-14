using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeoExpress.Abstractions
{
    public class ExpressContract
    {
        public class Parameter
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public class Function
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("return-type")]
            public string ReturnType { get; set; }

            [JsonProperty("parameters")]
            public List<Parameter> Parameters { get; set; }
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("entry-point")]
        public string EntryPoint { get; set; }

        [JsonProperty("contract-data")]
        public string ContractData { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, string> Properties { get; set; }

        [JsonProperty("functions")]
        public List<Function> Functions { get; set; }

        [JsonProperty("events")]
        public List<Function> Events { get; set; }
    }
}
