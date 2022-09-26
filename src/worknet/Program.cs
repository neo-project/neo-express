using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoWorkNet.Commands;
using static Neo.BlockchainToolkit.Utility;

namespace NeoWorkNet;

[Command("neoxp", Description = "Neo N3 blockchain private net for developers", UsePagerForHelpText = false)]
[VersionOption(ThisAssembly.AssemblyInformationalVersion)]
[Subcommand(typeof(CreateCommand), typeof(PrefetchCommand))]
partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        EnableAnsiEscapeSequences();

        var services = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .BuildServiceProvider();

        var app = new CommandLineApplication<Program>();
        app.Conventions
            .UseDefaultConventions()
            .AddConvention(new InputFileConvention())
            .UseConstructorInjection(services);

        try
        {
            return await app.ExecuteAsync(args);
        }
        catch (CommandParsingException ex)
        {
            await app.Error.WriteLineAsync($"\x1b[1m\x1b[31m\x1b[40m{ex.Message}");
            if (ex is UnrecognizedCommandParsingException uex && uex.NearestMatches.Any())
            {
                await app.Error.WriteLineAsync();
                await app.Error.WriteLineAsync("Did you mean this?");
                await app.Error.WriteLineAsync("    " + uex.NearestMatches.First());
            }
            return 1;
        }
        catch (Exception ex)
        {
            await app.Error.WriteLineAsync($"\x1b[1m\x1b[31m\x1b[40m{ex.GetType()}: {ex.Message}\x1b[0m").ConfigureAwait(false);
            return -1;
        }
    }

    internal int OnExecute(CommandLineApplication app, IConsole console)
    {
        console.WriteLine("You must specify a subcommand.");
        app.ShowHelp(false);
        return 1;
    }


}
