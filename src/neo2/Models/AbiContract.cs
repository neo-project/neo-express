using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeoExpress.Neo2.Models
{
    class AbiContract
    {
        public class ContractMetadata
        {
            [JsonProperty("title")]
            public string Title { get; set; } = string.Empty;

            [JsonProperty("description")]
            public string Description { get; set; } = string.Empty;

            [JsonProperty("version")]
            public string Version { get; set; } = string.Empty;

            [JsonProperty("author")]
            public string Author { get; set; } = string.Empty;

            [JsonProperty("email")]
            public string Email { get; set; } = string.Empty;

            [JsonProperty("has-storage")]
            public bool HasStorage { get; set; }

            [JsonProperty("has-dynamic-invoke")]
            public bool HasDynamicInvoke { get; set; }

            [JsonProperty("is-payable")]
            public bool IsPayable { get; set; }
        }

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

            [JsonProperty("parameters")]
            public List<Parameter> Parameters { get; set; } = new List<Parameter>();

            [JsonProperty("returntype")]
            public string ReturnType { get; set; } = string.Empty;
        }

        [JsonProperty("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonProperty("metadata")]
        public ContractMetadata? Metadata { get; set; }

        [JsonProperty("entrypoint")]
        public string Entrypoint { get; set; } = string.Empty;

        [JsonProperty("functions")]
        public List<Function> Functions { get; set; } = new List<Function>();

        [JsonProperty("events")]
        public List<Function> Events { get; set; } = new List<Function>();
    }
}
