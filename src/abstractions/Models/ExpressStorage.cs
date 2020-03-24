using Newtonsoft.Json;

namespace NeoExpress.Abstractions.Models
{
    public partial class ExpressStorage
    {
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;

        [JsonProperty("constant")]
        public bool Constant { get; set; }
    }
}
