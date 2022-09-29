using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using static Neo.BlockchainToolkit.Constants;

namespace NeoWorkNet;

partial class Program
{
    class InputFileConvention : IConvention
    {
        public void Apply(ConventionContext context)
        {
            if (context.ModelType is null) return;
            if (context.ModelType.Equals(typeof(Commands.CreateCommand))) return;

            var subCmdAttrib = context.ModelType.GetCustomAttribute<SubcommandAttribute>();
            if (subCmdAttrib is not null) return;

            var parser = context.Application.ValueParsers.GetParser<string>()
                ?? throw new InvalidOperationException("Can't get string value parser");

            var option = new CommandOption<string>(parser, "--input", CommandOptionType.SingleValue)
            {
                Description = $"Path to {WORKNET_EXTENSION} data file"
            };
            context.Application.AddOption(option);
        }
    }
}
