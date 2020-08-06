#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168

#pragma warning disable SA1200 // Using directives should be placed correctly
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
#pragma warning disable SA1649 // File name should match first type name

namespace Neo.Seattle.TraceDebug.Formatters
{
    using System;

    public class TraceDebugResolver : global::MessagePack.IFormatterResolver
    {
        public static readonly global::MessagePack.IFormatterResolver Instance = new TraceDebugResolver();

        private TraceDebugResolver()
        {
        }

        public global::MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly global::MessagePack.Formatters.IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                var f = TraceDebugResolverGetFormatterHelper.GetFormatter(typeof(T));
                if (f != null)
                {
                    Formatter = (global::MessagePack.Formatters.IMessagePackFormatter<T>)f;
                }
            }
        }
    }

    internal static class TraceDebugResolverGetFormatterHelper
    {
        private static readonly global::System.Collections.Generic.Dictionary<Type, int> lookup;

        static TraceDebugResolverGetFormatterHelper()
        {
            lookup = new global::System.Collections.Generic.Dictionary<Type, int>(11)
            {
                { typeof(global::System.Collections.Generic.IReadOnlyDictionary<byte[], global::Neo.Ledger.StorageItem>), 0 },
                { typeof(global::System.Collections.Generic.IReadOnlyDictionary<global::Neo.UInt160, global::System.Collections.Generic.IReadOnlyDictionary<byte[], global::Neo.Ledger.StorageItem>>), 1 },
                { typeof(global::System.Collections.Generic.IReadOnlyList<global::Neo.Seattle.TraceDebug.Models.StackFrame>), 2 },
                { typeof(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>), 3 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.ITraceRecord), 4 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Fault), 5 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Log), 6 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Notify), 7 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Results), 8 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.StackFrame), 9 },
                { typeof(global::Neo.Seattle.TraceDebug.Models.TracePoint), 10 },
                { typeof(global::Neo.VM.Types.StackItem), 11 },
                { typeof(global::Neo.Ledger.StorageItem), 12 },
                { typeof(global::Neo.UInt160), 13 },
            };
        }

        internal static object GetFormatter(Type t)
        {
            int key;
            if (!lookup.TryGetValue(t, out key))
            {
                return null;
            }

            switch (key)
            {
                case 0: return new global::MessagePack.Formatters.InterfaceReadOnlyDictionaryFormatter<byte[], global::Neo.Ledger.StorageItem>();
                case 1: return new global::MessagePack.Formatters.InterfaceReadOnlyDictionaryFormatter<UInt160, global::System.Collections.Generic.IReadOnlyDictionary<byte[], global::Neo.Ledger.StorageItem>>();
                case 2: return new global::MessagePack.Formatters.InterfaceReadOnlyListFormatter<global::Neo.Seattle.TraceDebug.Models.StackFrame>();
                case 3: return new global::MessagePack.Formatters.InterfaceReadOnlyListFormatter<global::Neo.VM.Types.StackItem>();
                case 4: return new global::Neo.Seattle.TraceDebug.Formatters.ITraceRecordFormatter();
                case 5: return new global::Neo.Seattle.TraceDebug.Formatters.FaultFormatter();
                case 6: return new global::Neo.Seattle.TraceDebug.Formatters.LogFormatter();
                case 7: return new global::Neo.Seattle.TraceDebug.Formatters.NotifyFormatter();
                case 8: return new global::Neo.Seattle.TraceDebug.Formatters.ResultsFormatter();
                case 9: return new global::Neo.Seattle.TraceDebug.Formatters.StackFrameFormatter();
                case 10: return new global::Neo.Seattle.TraceDebug.Formatters.TracePointFormatter();
                case 11: return new global::Neo.Seattle.TraceDebug.Formatters.StackItemFormatter();
                case 12: return new global::Neo.Seattle.TraceDebug.Formatters.StorageItemFormatter();
                case 13: return new global::Neo.Seattle.TraceDebug.Formatters.UInt160Formatter();
                default: return null;
            }
        }
    }
}

#pragma warning restore 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612

#pragma warning restore SA1312 // Variable names should begin with lower-case letter
#pragma warning restore SA1200 // Using directives should be placed correctly
#pragma warning restore SA1649 // File name should match first type name
