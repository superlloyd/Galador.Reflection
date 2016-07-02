using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    public static class WeakEvents
    {
        public static PropertyChangedEventHandler AddWeakHandler<T>(this INotifyPropertyChanged model, T target, Action<T, PropertyChangedEventArgs> action)
            where T : class
        {
            var weakRef = new WeakReference(target);
            PropertyChangedEventHandler handler = null;
            handler = new PropertyChangedEventHandler(
                (s, e) =>
                {
                    var strongRef = weakRef.Target as T;
                    if (strongRef != null)
                    {
                        action(strongRef, e);
                    }
                    else
                    {
                        model.PropertyChanged -= handler;
                        handler = null;
                    }
                });
            model.PropertyChanged += handler;
            return handler;
        }

        public static EventHandler<TArgs> SetHandler<T, TArgs>(T subscriber,
                        Action<EventHandler<TArgs>> add, Action<EventHandler<TArgs>> remove,
                        Action<T, TArgs> action)
                        where TArgs : EventArgs
                        where T : class
        {
            var weakRef = new WeakReference(subscriber);
            EventHandler<TArgs> handler = null;
            handler = new EventHandler<TArgs>(
                (s, e) =>
                {
                    var strongRef = weakRef.Target as T;
                    if (strongRef != null)
                    {
                        action(strongRef, e);
                    }
                    else
                    {
                        remove(handler);
                        handler = null;
                    }
                });
            add(handler);
            return handler;
        }

        public static EventHandler SetHandler<T>(T subscriber,
                        Action<EventHandler> add, Action<EventHandler> remove,
                        Action<T> action)
                        where T : class
        {
            var weakRef = new WeakReference(subscriber);
            EventHandler handler = null;
            handler = new EventHandler(
                (s, e) =>
                {
                    var strongRef = weakRef.Target as T;
                    if (strongRef != null)
                    {
                        action(strongRef);
                    }
                    else
                    {
                        remove(handler);
                        handler = null;
                    }
                });
            add(handler);
            return handler;
        }
    }
}
