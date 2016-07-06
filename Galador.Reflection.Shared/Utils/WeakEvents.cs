using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Helper class to create weak events. i.e. event handlers that do not prevent their target to be garbage collected.
    /// </summary>
    public static class WeakEvents
    {
        /// <summary>
        /// Help listening to <see cref="INotifyPropertyChanged"/> in a weak fashion.
        /// </summary>
        /// <typeparam name="T">The target type</typeparam>
        /// <param name="model">The model, source of the event.</param>
        /// <param name="target">The target, implementing the even handler.</param>
        /// <param name="action">Action to be called on a (the) target. Do not use the current target variable but use the action parameter instead, 
        /// so as to not capture the target in the handler, preventing garbage collection.</param>
        /// <returns>The registered handler, for early removal.</returns>
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

        /// <summary>
        /// Help listening to a <see cref="EventHandler{TEventArgs}"/> in a weak fashion.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments.</typeparam>
        /// <param name="subscriber">The target, implementing the even handler.</param>
        /// <param name="add">How to register an event handler with the event source.</param>
        /// <param name="remove">How to unregister an event with the event source.</param>
        /// <param name="action">The handler to be called when the event is triggered.</param>
        /// <returns>The registered handler, for early removal.</returns>
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

        /// <summary>
        /// Help listening to a <see cref="EventHandler"/> in a weak fashion.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="subscriber">The target, implementing the even handler.</param>
        /// <param name="add">How to register an event handler with the event source.</param>
        /// <param name="remove">How to unregister an event with the event source.</param>
        /// <param name="action">The handler to be called when the event is triggered.</param>
        /// <returns>The registered handler, for early removal.</returns>
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
