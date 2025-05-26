// Copyright (C) 2015-2024 The Neo Project.
//
// ISnapshot.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;

namespace Neo.BlockchainToolkit.Persistence
{
    /// <summary>
    /// Legacy ISnapshot interface for backward compatibility.
    /// This interface bridges the gap between old neo-express snapshot implementations
    /// and the new Neo 3.8.2 IStoreSnapshot interface.
    /// </summary>
    [Obsolete("Use IStoreSnapshot instead. This interface is provided for backward compatibility only.")]
    public interface ISnapshot : IDisposable
    {
        /// <summary>
        /// Reads a specified entry from the snapshot.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <returns>The data of the entry. Or null if it doesn't exist.</returns>
        [Obsolete("use TryGet(byte[] key, out byte[]? value) instead.")]
        byte[]? TryGet(byte[]? key);

        /// <summary>
        /// Reads a specified entry from the snapshot.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="value">The data of the entry.</param>
        /// <returns>true if the entry exists; otherwise, false.</returns>
        bool TryGet(byte[]? key, out byte[]? value);

        /// <summary>
        /// Determines whether the snapshot contains the specified entry.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <returns>true if the snapshot contains an entry with the specified key; otherwise, false.</returns>
        bool Contains(byte[]? key);

        /// <summary>
        /// Finds the entries starting with the specified prefix.
        /// </summary>
        /// <param name="key">The prefix of the key.</param>
        /// <param name="direction">The search direction.</param>
        /// <returns>The entries found with the desired prefix.</returns>
        [Obsolete("use Find(byte[]? key_prefix, SeekDirection direction) instead.")]
        IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? key, SeekDirection direction);

        /// <summary>
        /// Finds the entries starting with the specified prefix.
        /// </summary>
        /// <param name="key_prefix">The prefix of the key.</param>
        /// <param name="direction">The search direction.</param>
        /// <returns>The entries found with the desired prefix.</returns>
        IEnumerable<(byte[] Key, byte[] Value)> Find(byte[]? key_prefix = null, SeekDirection direction = SeekDirection.Forward);

        /// <summary>
        /// Puts an entry to the snapshot.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="value">The data of the entry.</param>
        void Put(byte[]? key, byte[]? value);

        /// <summary>
        /// Deletes an entry from the snapshot.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        void Delete(byte[]? key);

        /// <summary>
        /// Commits all changes in the snapshot to the database.
        /// </summary>
        void Commit();
    }
}
