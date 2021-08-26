using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("policy", Description = "Manage blockchain policy")]
    [Subcommand(typeof(Get), typeof(Set))]

    partial class PolicyCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}