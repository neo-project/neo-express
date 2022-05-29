using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace NeoExpress
{
    static class CommandLineAppExtensions
    {
        static object[] BindParameters(Delegate @delegate, IServiceProvider provider, CancellationToken token = default)
        {
            var paramInfos = @delegate.Method.GetParameters() ?? throw new Exception();
            var @params = new object[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                @params[i] = paramInfos[i].ParameterType == typeof(CancellationToken)
                    ? token : provider.GetRequiredService(paramInfos[i].ParameterType);
            }
            return @params;
        }

        public static int Execute(this CommandLineApplication app, Delegate @delegate)
        {
            if (@delegate.Method.ReturnType != typeof(void)) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                @delegate.DynamicInvoke(@params);
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

        public static async Task<int> ExecuteAsync(this CommandLineApplication app, Delegate @delegate, CancellationToken token = default)
        {
            if (@delegate.Method.ReturnType != typeof(Task)) throw new Exception();
            if (@delegate.Method.ReturnType.GenericTypeArguments.Length > 0) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                var @return = @delegate.DynamicInvoke(@params) ?? throw new Exception();
                await ((Task)@return).ConfigureAwait(false);
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

        public static IFileSystem GetFileSystem(this CommandLineApplication app)
        {
            return ((IServiceProvider)app).GetRequiredService<IFileSystem>();
        }

        public static IExpressChain GetExpressFile(this CommandLineApplication app)
        {
            var option = app.GetOptions().Single(o => o.LongName == "input");
            var input = option.Value() ?? string.Empty;
            var fileSystem = app.GetFileSystem();
            return new ExpressChain(input, fileSystem);
        }
    }
}
