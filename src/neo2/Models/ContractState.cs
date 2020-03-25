using System;
using Newtonsoft.Json;

namespace NeoExpress.Neo2.Models
{
    class ContractState
    {
        public class PropertyInfo
        {
            [JsonProperty("storage")]
            public bool Storage { get; set; }

            [JsonProperty("dynamic_invoke")]
            public bool DynamicInvoke { get; set; }
        }

        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonProperty("script")]
        public string Script { get; set; }  = string.Empty;

        [JsonProperty("parameters")]
        public string[] Parameters { get; set; } = Array.Empty<string>();

        [JsonProperty("returntype")]
        public string ReturnType { get; set; }  = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("code_version")]
        public string CodeVersion { get; set; } = string.Empty;

        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("properties")]
        public PropertyInfo Properties { get; set; } = new PropertyInfo();
    }
}
