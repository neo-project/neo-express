using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Backend2;
using Newtonsoft.Json;

namespace Neo.Express.Commands
{
    [Command("create")]
    internal class CreateCommand
    {
        [AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
        private class ValidNodeCountAttribute : ValidationAttribute
        {
            public ValidNodeCountAttribute() : base("The value for {0} must be 1, 4 or 7")
            {
            }

            protected override ValidationResult IsValid(object value, ValidationContext context)
            {
                if (value == null || (value is string str && str != "1" && str != "4" && str != "7"))
                {
                    return new ValidationResult(FormatErrorMessage(context.DisplayName));
                }

                return ValidationResult.Success;
            }
        }

        [ValidNodeCount]
        [Option]
        private int Count { get; }

        [Option]
        private string Output { get; }

        [Option]
        private bool Force { get; }

        [Option]
        [Range(49152, ushort.MaxValue)]
        private ushort Port { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var output = Program.GetDefaultFilename(Output);
                if (File.Exists(output) && !Force)
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }

                var count = (Count == 0 ? 1 : Count);
                var port = Port == 0 ? (ushort)49152 : Port;
                var backend = Program.GetBackend();
                var chain = backend.CreateBlockchain(count, port);

                var serializer = new JsonSerializer();
                using (var stream = File.Open(output, FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    serializer.Serialize(writer, chain);
                }

                console.WriteLine($"Created {count} node privatenet at {output}");
                console.WriteWarning("    Note: The private keys for the accounts in this file are are *not* encrypted.");
                console.WriteWarning("          Do not use these accounts on MainNet or in any other system where security is a concern.");
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
