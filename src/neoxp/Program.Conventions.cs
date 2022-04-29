using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;

namespace NeoExpress
{
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

                var option = new CommandOption<string>(parser, "--input", CommandOptionType.SingleValue);
                context.Application.AddOption(option);
            }
        }

        class StackTraceConvention : IConvention
        {
            public void Apply(ConventionContext context)
            {
                if (context.ModelType is null) return;

                var subCmdAttrib = context.ModelType.GetCustomAttribute<SubcommandAttribute>();
                if (subCmdAttrib is not null) return;

                var parser = context.Application.ValueParsers.GetParser<bool>()
                    ?? throw new InvalidOperationException("Can't get boolean value parser");

                var option = new CommandOption<bool>(parser, "--stack-trace", CommandOptionType.NoValue)
                {
                    ShowInHelpText = false
                };
                context.Application.AddOption(option);
            }
        }
    }
}
