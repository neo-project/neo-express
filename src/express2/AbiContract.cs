using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeoExpress.Neo2Backend
{
    public class AbiContract
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

            [JsonProperty("parameters")]
            public List<Parameter> Parameters { get; set; }

            [JsonProperty("returntype")]
            public string ReturnType { get; set; }
        }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("entrypoint")]
        public string Entrypoint { get; set; }

        [JsonProperty("functions")]
        public List<Function> Functions { get; set; }

        [JsonProperty("events")]
        public List<Function> Events { get; set; }
    }
}
