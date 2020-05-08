using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    public class InvokeResponse
    {
        public class Stack
        {
            [JsonProperty("type")]
            public string Type { get; set; } = string.Empty;

            [JsonProperty("value")]
            public string Value { get; set; } = string.Empty;
        }

        [JsonProperty("script")]
        public string Script { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string State { get; set; } = string.Empty;

        [JsonProperty("gas_consumed")]
        public decimal GasConsumed { get; set; }

        [JsonProperty("stack")]
        public Stack[] ReturnStack { get; set; } = Array.Empty<Stack>();

        [JsonProperty("tx")]
        public string Tx { get; set; } = string.Empty;
    }
}
