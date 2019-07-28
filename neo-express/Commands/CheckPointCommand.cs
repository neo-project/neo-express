using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using RocksDbSharp;

namespace Neo.Express.Commands
{
    [Command("checkpoint")]
    class CheckPointCommand
    {
        [Option]
        private string Input { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (devChain, _) = DevChain.Load(Input);

                var consensusNode = devChain.ConsensusNodes[0];

                const string ROOT_PATH = @"C:\Users\harry\neoexpress";
                var path = Path.Combine(ROOT_PATH, consensusNode.Wallet.GetAccounts().Single(a => a.IsDefault).Address);

                string checkpointTempPath = Path.Combine(
                    Path.GetTempPath(), Path.GetRandomFileName());

                using (var db = new Persistence.DevStore(path))
                {
                    db.CheckPoint(checkpointTempPath);
                    System.IO.Compression.ZipFile.CreateFromDirectory(
                        checkpointTempPath,
                        Path.Combine(ROOT_PATH, $"express-checkpoint-{DateTimeOffset.Now:yyyyMMdd-hhmmss}.zip"));
                }

                Directory.Delete(checkpointTempPath, true);

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
