using System;

namespace NeoExpress.Node
{

    public partial class ExpressSystem
    {
        // EventWrapper class receives events from the Akka EventStream and invokes the provided callback.
        // Code taken from neo-gui project:
        //   https://github.com/neo-project/neo-node/blob/master/neo-gui/IO/Actors/EventWrapper.cs
        internal class EventWrapper<T> : Akka.Actor.UntypedActor
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

            public static Akka.Actor.Props Props(Action<T> callback)
            {
                return Akka.Actor.Props.Create(() => new EventWrapper<T>(callback));
            }
        }
    }
}
