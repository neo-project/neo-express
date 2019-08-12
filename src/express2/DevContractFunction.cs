using Neo.Ledger;
using Neo.SmartContract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Express.Backend2
{
    public class DevContractFunction
    {
        public string Name { get; set; }
        public List<(string name, ContractParameterType type)> Parameters { get; set; }
        public ContractParameterType ReturnType { get; set; }

        private static string TypeToString(ContractParameterType type) => Enum.GetName(typeof(ContractParameterType), type);

        public void ToJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(Name);
            writer.WritePropertyName("returntype");
            writer.WriteValue(TypeToString(ReturnType));

            writer.WritePropertyName("parameters");
            writer.WriteStartArray();
            foreach (var (name, type) in Parameters)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                writer.WriteValue(name);
                writer.WritePropertyName("type");
                writer.WriteValue(TypeToString(type));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public static DevContractFunction FromJson(JToken json)
        {
            ContractParameterType TypeParse(JToken jtoken) => Enum.Parse<ContractParameterType>(jtoken.Value<string>());

            var name = json.Value<string>("name");
            var retType = TypeParse(json["returntype"]);
            var @params = json["parameters"].Select(j => (j.Value<string>("name"), TypeParse(j["type"])));

            return new DevContractFunction
            {
                Name = name,
                Parameters = @params.ToList(),
                ReturnType = retType
            };
        }
    }
}
