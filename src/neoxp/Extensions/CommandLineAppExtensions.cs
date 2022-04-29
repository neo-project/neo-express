using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace NeoExpress
{
    static class CommandLineAppExtensions
    {
        public static int TryExecute(this CommandLineApplication app, Action action)
        {
            try
            {
                action();
                return 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                app.WriteException(ex.InnerException);
                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        public static int Execute(this CommandLineApplication app, Delegate @delegate)
        {
            var type = @delegate.GetType();
            var invokeMethod = type.GetMethod("Invoke") ?? throw new Exception();
            var invokeParams = invokeMethod.GetParameters()  ?? throw new Exception();

            var @params = new object[invokeParams.Length];

            var provider = (IServiceProvider)app;
            for (int i = 0; i < invokeParams.Length; i++)
            {
                @params[i] = provider.GetRequiredService(invokeParams[i].ParameterType);
            }

            return TryExecute(app, () => invokeMethod.Invoke(@delegate, @params));
        }

        public static IFileSystem GetFileSystem(this CommandLineApplication app)
        {
            return ((IServiceProvider)app).GetRequiredService<IFileSystem>();
        }

        public static IExpressFile GetExpressFile(this CommandLineApplication app)
        {
            var option = app.GetOptions().Single(o => o.LongName == "input");
            var input = option.Value() ?? string.Empty;
            var fileSystem = app.GetFileSystem();
            return new ExpressFile(input, fileSystem);
        }
    }
}
