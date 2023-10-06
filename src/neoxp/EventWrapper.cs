// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Akka.Actor;
using System;

namespace NeoExpress
{
    // EventWrapper class taken from neo-gui project: https://github.com/neo-project/neo-node/blob/master/neo-gui/IO/Actors/EventWrapper.cs
    class EventWrapper<T> : UntypedActor
    {
        readonly Action<T> callback;

        public EventWrapper(Action<T> callback)
        {
            this.callback = callback;
            Context.System.EventStream.Subscribe(Self, typeof(T));
        }

        protected override void OnReceive(object message)
        {
            if (message is T obj)
                callback(obj);
        }

        protected override void PostStop()
        {
            Context.System.EventStream.Unsubscribe(Self);
            base.PostStop();
        }

        public static Props Props(Action<T> callback)
        {
            return Akka.Actor.Props.Create(() => new EventWrapper<T>(callback));
        }
    }
}
