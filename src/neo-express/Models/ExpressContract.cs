using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeoExpress.Models
{
    public class ExpressContract
    {
        public class Parameter
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("type")]
            public string Type { get; set; } = string.Empty;
        }

        public class Function
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("return-type")]
            public string ReturnType { get; set; } = string.Empty;

            [JsonProperty("parameters")]
            public List<Parameter> Parameters { get; set; } = new List<Parameter>();
        }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonProperty("entry-point")]
        public string EntryPoint { get; set; } = string.Empty;

        [JsonProperty("contract-data")]
        public string ContractData { get; set; } = string.Empty;

        [JsonProperty("properties")]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        [JsonProperty("functions")]
        public List<Function> Functions { get; set; } = new List<Function>();

        [JsonProperty("events")]
        public List<Function> Events { get; set; } = new List<Function>();
    }
}
