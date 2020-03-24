using System;
using System.IO;
using System.Linq;
using System.Text;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoExpress
{
    internal static class Extensions
    {
        static void WriteMessage(IConsole console, string message, ConsoleColor color)
        {
            var currentColor = console.ForegroundColor;
            try
            {
                console.ForegroundColor = color;
                console.WriteLine(message);
            }
            finally
            {
                console.ForegroundColor = currentColor;
            }
        }

        public static void WriteError(this IConsole console, string message)
        {
            WriteMessage(console, message, ConsoleColor.Red);
        }

        public static void WriteWarning(this IConsole console, string message)
        {
            WriteMessage(console, message, ConsoleColor.Yellow);
        }
    }
}
