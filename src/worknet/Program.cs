using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoWorkNet.Commands;
using static Crayon.Output;

namespace NeoWorkNet;

[Command("neo-worknet", Description = "Branch a public Neo N3 blockchain for private development use", UsePagerForHelpText = false)]
[VersionOption(ThisAssembly.AssemblyInformationalVersion)]
[Subcommand(typeof(CreateCommand), typeof(PrefetchCommand), typeof(ResetCommand), typeof(RunCommand))]
partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        Crayon.Output.Enable();

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
            await app.Error.WriteLineAsync(Bright.Red(ex.Message));
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
            await app.Error.WriteLineAsync(Bright.Red($"{ex.GetType()}: {ex.Message}")).ConfigureAwait(false);
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
