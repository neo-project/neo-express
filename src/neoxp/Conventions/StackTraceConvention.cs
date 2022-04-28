using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;

namespace NeoExpress
{
    class StackTraceConvention : IConvention
    {
        public void Apply(ConventionContext context)
        {
            if (context.ModelType is null) return;

            var subCmdAttrib = context.ModelType.GetCustomAttribute<SubcommandAttribute>();
            if (subCmdAttrib is not null) return;

            var boolParser = context.Application.ValueParsers.GetParser<bool>()
                ?? throw new InvalidOperationException("Can't get boolean value parser");

            var option = new CommandOption<bool>(boolParser, "--stack-trace", CommandOptionType.NoValue);
            option.ShowInHelpText = false;
            
            context.Application.AddOption(option);
        }
    }
}
