using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;

namespace NeoExpress
{
    partial class Program
    {
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
