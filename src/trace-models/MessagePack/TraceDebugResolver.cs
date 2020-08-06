using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Neo.Ledger;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.Seattle.TraceDebug.Formatters
{
    public sealed class TraceDebugResolver : IFormatterResolver
    {
        public static readonly TraceDebugResolver Instance = new TraceDebugResolver();

        private static readonly Dictionary<Type, IMessagePackFormatter> formatterMap = new Dictionary<Type, IMessagePackFormatter>()
        {
            { typeof(StackItem), StackItemFormatter.Instance },
            { typeof(StorageItem), StorageItemFormatter.Instance },
            { typeof(UInt160), UInt160Formatter.Instance },
        };

        private TraceDebugResolver()
        {
        }

        public static IMessagePackFormatter<T>? GetFormatterHelper<T>()
        {
            if (formatterMap.TryGetValue(typeof(T), out var formatter))
            {
                return (IMessagePackFormatter<T>)formatter;
            }

            return global::MessagePack.Resolvers.StandardResolver.Instance.GetFormatter<T>();
        }

        public IMessagePackFormatter<T>? GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T>? Formatter;

            static FormatterCache()
            {
                Formatter = GetFormatterHelper<T>();
            }
        }
    }
}
