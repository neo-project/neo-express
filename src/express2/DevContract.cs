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
    public class DevContract
    {
        public string Name { get; set; }
        public UInt160 Hash { get; set; }
        public string EntryPoint { get; set; }
        public byte[] ContractData { get; set; }
        public List<DevContractFunction> Functions { get; set; }
        public List<DevContractFunction> Events { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Email { get; set; }
        public string Version { get; set; }
        public ContractPropertyState ContractPropertyState { get; set; }

        public void ToJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(Name);
            writer.WritePropertyName("hash");
            writer.WriteValue(Hash.ToString());
            writer.WritePropertyName("entrypoint");
            writer.WriteValue(EntryPoint);
            writer.WritePropertyName("contract-data");
            writer.WriteValue(ContractData.ToHexString());
            writer.WritePropertyName("title");
            writer.WriteValue(Title);
            writer.WritePropertyName("description");
            writer.WriteValue(Description);
            writer.WritePropertyName("version");
            writer.WriteValue(Version);
            writer.WritePropertyName("author");
            writer.WriteValue(Author);
            writer.WritePropertyName("email");
            writer.WriteValue(Email);
            writer.WritePropertyName("contract-property-state");
            writer.WriteValue((byte)ContractPropertyState);

            writer.WritePropertyName("functions");
            writer.WriteStartArray();
            foreach (var function in Functions)
            {
                function.ToJson(writer);
            }
            writer.WriteEndArray();

            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var @event in Events)
            {
                @event.ToJson(writer);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static DevContract FromJson(JToken json)
        {
            var name = json.Value<string>("name");
            var contractData = json.Value<string>("contract-data").HexToBytes();
            var hash = UInt160.Parse(json.Value<string>("hash"));
            var entryPoint = json.Value<string>("entrypoint");
            var functions = json["functions"].Select(DevContractFunction.FromJson);
            var events = json["events"].Select(DevContractFunction.FromJson);

            var title = json.Value<string>("title");
            var description = json.Value<string>("description");
            var version = json.Value<string>("version");
            var author = json.Value<string>("author");
            var email = json.Value<string>("email");
            var contractPropertyState = (ContractPropertyState)json.Value<byte>("contract-property-state");

            return new DevContract
            {
                Name = name,
                Hash = hash,
                EntryPoint = entryPoint,
                ContractData = contractData,
                Functions = functions.ToList(),
                Events = events.ToList(),
                Title = title,
                Description = description,
                Author = author,
                Email = email,
                Version = version,
                ContractPropertyState = contractPropertyState
            };
        }

        static (UInt160 hash, string entrypoint, List<DevContractFunction> functions, List<DevContractFunction> events) LoadAbi(string abiFile)
        {
            using (var stream = File.OpenRead(abiFile))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                var json = JObject.Load(reader);

                var hash = UInt160.Parse(json.Value<string>("hash"));
                var entryPoint = json.Value<string>("entrypoint");
                var functions = json["functions"].Select(DevContractFunction.FromJson).ToList();
                var events = json["events"].Select(DevContractFunction.FromJson).ToList();

                return (hash, entryPoint, functions, events);
            }
        }

        static (string title, string description, string version, string author, string email, ContractPropertyState contractPropertyState) LoadMetadata(string mdFile)
        {
            using (var stream = File.OpenRead(mdFile))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                var json = JObject.Load(reader);

                var title = json.Value<string>("title");
                var description = json.Value<string>("description");
                var version = json.Value<string>("version");
                var author = json.Value<string>("author");
                var email = json.Value<string>("email");

                var contractPropertyState = ContractPropertyState.NoProperty;
                if (json.Value<bool>("has-storage")) contractPropertyState |= ContractPropertyState.HasStorage;
                if (json.Value<bool>("has-dynamic-invoke")) contractPropertyState |= ContractPropertyState.HasDynamicInvoke;
                if (json.Value<bool>("is-payable")) contractPropertyState |= ContractPropertyState.Payable;

                return (title, description, version, author, email, contractPropertyState);
            }
        }

        public static DevContract Load(string avnFile, string abiFile, ContractPropertyState contractPropertyState)
        {
            var name = Path.GetFileNameWithoutExtension(avnFile);
            var contractData = File.ReadAllBytes(avnFile);
            var abi = LoadAbi(abiFile);

            return new DevContract
            {
                Name = name,
                Hash = abi.hash,
                EntryPoint = abi.entrypoint,
                ContractData = contractData,
                Functions = abi.functions.ToList(),
                Events = abi.events.ToList(),
                Title = Path.GetFileNameWithoutExtension(avnFile),
                Description = "No description provided",
                Author = "No author provided",
                Email = "nobody@fake.email",
                Version = "0.0.0",
                ContractPropertyState = contractPropertyState
            };
        }

        public static DevContract Load(string avnFile, string abiFile, string mdFile)
        {
            var name = Path.GetFileNameWithoutExtension(avnFile);
            var contractData = File.ReadAllBytes(avnFile);
            var abi = LoadAbi(abiFile);
            var md = LoadMetadata(mdFile);

            return new DevContract
            {
                Name = name,
                Hash = abi.hash,
                EntryPoint = abi.entrypoint,
                ContractData = contractData,
                Functions = abi.functions.ToList(),
                Events = abi.events.ToList(),
                Title = md.title,
                Description = md.description,
                Author = md.author,
                Email = md.email,
                Version = md.version,
                ContractPropertyState = md.contractPropertyState
            };
        }
    }
}
