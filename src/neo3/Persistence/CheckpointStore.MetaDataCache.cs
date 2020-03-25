using Neo.IO;
using OneOf;
using RocksDbSharp;
using System;

namespace NeoExpress.Neo3.Persistence
{
    internal partial class CheckpointStore
    {
        private class MetaDataCache<T> : Neo.IO.Caching.MetaDataCache<T>
            where T : class, ICloneable<T>, ISerializable, new()
        {
            private readonly MetadataTracker<T> tracker;
            private readonly OneOf<T, OneOf.Types.None>? snapshot = null;
            private readonly Action<T>? updater = null;

            public MetaDataCache(MetadataTracker<T> tracker, Func<T>? factory = null) : base(factory)
            {
                this.tracker = tracker;
            }

            public MetaDataCache(MetadataTracker<T> tracker, OneOf<T, OneOf.Types.None> snapshot, Action<T> updater, Func<T>? factory = null)
                : base(factory)
            {
                this.tracker = tracker;
                this.snapshot = snapshot;
                this.updater = updater;
            }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
            protected override T? TryGetInternal()
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
            {
                return tracker.TryGet(snapshot);
            }

            protected override void AddInternal(T item)
            {
                UpdateInternal(item);
            }

            protected override void UpdateInternal(T item)
            {
                if (updater == null)
                    throw new InvalidOperationException();

                updater(item);
            }
        }
    }
}
