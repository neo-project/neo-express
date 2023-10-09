// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Plugins
{
    public readonly record struct NotificationInfo(
        uint BlockIndex,
        ushort TxIndex,
        ushort NotificationIndex,
        NotificationRecord Notification);

    public interface INotificationsProvider
    {
        IEnumerable<NotificationInfo> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160> contracts = null,
            IReadOnlySet<string> eventNames = null);
    }
}
