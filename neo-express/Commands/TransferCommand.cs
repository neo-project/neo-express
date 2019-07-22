using McMaster.Extensions.CommandLineUtils;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Neo.Express.Commands
{
    [Command("transfer")]
    class TransferCommand
    {
        [Argument(0)]
        string Asset { get; }

        [Argument(1)]
        string Quantity { get; }

        [Argument(2)]
        string Sender { get; }

        [Argument(3)]
        string Receiver { get; }

        [Option]
        string Input { get; }


        async Task<HttpResponseMessage> PostAsync(string uri)
        {
            using (var stream = new MemoryStream())
            {
                //using (var writer = new Utf8JsonWriter(stream))
                //{
                //    writer.WriteStartObject();
                //    writer.WriteNumber("id", 1);
                //    writer.WriteString("jsonrpc", "2.0");
                //    writer.WriteString("method", "express-transfer");
                //    writer.WriteStartArray();
                //    writer.WriteStringValue(Asset);
                //    writer.WriteStringValue(Quantity);
                //    writer.WriteStringValue(Sender);
                //    writer.WriteStringValue(Receiver);
                //    writer.WriteEndArray();
                //    writer.WriteEndObject();
                //}

                var client = new HttpClient();
                return (await client.PostAsync("", new StreamContent(stream)))
                    .EnsureSuccessStatusCode();
            }
        }

        async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            var input = Program.DefaultPrivatenetFileName(Input);
            if (!File.Exists(input))
            {
                console.WriteLine($"{input} doesn't exist");
                app.ShowHelp();
                return 1;
            }

            var devchain = DevChain.Load(input);

            var response = await PostAsync("");
            //using (var stream = await response.Content.ReadAsStreamAsync())
            //using (var json = JsonDocument.Parse(stream))
            //{
            //    console.WriteLine(json.ToString());
            //}

            return 1;
        }
    }
}
