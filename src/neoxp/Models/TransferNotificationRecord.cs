using System;
using System.Numerics;
using Neo;
using Neo.VM.Types;
using NeoExpress.Models;

namespace NeoExpress.Models
{
    class TransferNotificationRecord
    {
        public readonly UInt160 Asset;
        public readonly UInt160 From;
        public readonly UInt160 To;
        public readonly BigInteger Amount;
        public readonly ReadOnlyMemory<byte> TokenId;
        public readonly NotificationRecord Notification;

        public TransferNotificationRecord(UInt160 asset, UInt160 from, UInt160 to, BigInteger amount, ReadOnlyMemory<byte> tokenId, NotificationRecord notification)
        {
            Asset = asset;
            From = from;
            To = to;
            TokenId = tokenId;
            Amount = amount;
            Notification = notification;
        }

        public static TransferNotificationRecord? Create(NotificationRecord notification)
        {
            if (notification.State.Count < 3) return null;

            var from = ParseAddress(notification.State[0]);
            if (from == null) return null;
            var to = ParseAddress(notification.State[1]);
            if (to == null) return null;
            if (from == UInt160.Zero && to == UInt160.Zero) return null;

            var amountItem = notification.State[2];
            if (amountItem is not ByteString && amountItem is not Integer) return null;
            var amount = amountItem.GetInteger();

            var asset = notification.ScriptHash;
            return notification.State.Count switch
            {
                3 => new TransferNotificationRecord(asset, from, to, amount, default, notification),
                4 when (notification.State[3] is Neo.VM.Types.ByteString tokenId)
                    => new TransferNotificationRecord(asset, from, to, amount, tokenId, notification),
                _ => null
            };

            // returning null from ParseAddress implies invalid address value
            // A null StackItem address is valid and gets translated as UInt160.Zero
            static UInt160? ParseAddress(StackItem item)
            {
                if (!item.IsNull && item is not ByteString) return null;
                if (item.IsNull) return UInt160.Zero;
                var span = item.GetSpan();
                if (span.Length != UInt160.Length) return null;
                return new UInt160(span);
            }
        }
    }
}
