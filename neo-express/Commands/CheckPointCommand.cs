using McMaster.Extensions.CommandLineUtils;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using RocksDbSharp;
using System.IO.Compression;
using System;
using System.IO;

namespace Neo.Express.Commands
{
    [Command("checkpoint")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    internal partial class CheckPointCommand
    {
        private const string CHECKPOINT_EXTENSION = ".neoexpress-checkpoint";
        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteError("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        private static void ValidateCheckpointAddress(string filename, DevConsensusNode account)
        {
            var archive = ZipFile.OpenRead(filename);
            var entry = archive.GetEntry(ADDRESS_FILENAME);
            if (entry == null)
            {
                throw new Exception($"{Path.GetFileName(filename)} is not a valid neo-express checkpoint file");
            }

            using (var reader = new StreamReader(entry.Open()))
            {
                var address = reader.ReadToEnd();
                if (address != account.Wallet.DefaultAccount.Address)
                {
                    throw new Exception($"{Path.GetFileName(filename)} is a checkpoint for a different blockchain ({address})");
                }
            }
        }

        private static string ValidateCheckpointFileName(string name)
        {
            var filename = name;

            if (!File.Exists(filename))
            {
                filename = name + CHECKPOINT_EXTENSION;
            }

            if (!File.Exists(filename))
            {
                throw new Exception($"Checkpoint {name} couldn't be found");
            }

            return filename;
        }
    }
}
