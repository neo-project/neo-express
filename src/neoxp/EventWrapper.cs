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
            if (message is T obj) callback(obj);
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
