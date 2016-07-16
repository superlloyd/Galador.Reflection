using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Collections;

#if __NET__

using System.Configuration;

#endif

namespace Galador.Reflection.Logging
{
    /// <summary>
    /// The class that hold reference to all <see cref="TraceKey"/>, which are very thin wrapper around <c>System.Diagnostic.Trace</c>.
    /// </summary>
    /// <remarks>
    /// All <see cref="TraceKey.IsEnabled"/> state can be initialized in th <c>App.config</c> file on the full .NET framework.
    /// With the key <c>"TraceKeys." + trace.Name</c>, and value <c>true, false, on, off</c>
    /// </remarks>
    public static class TraceKeys
    {
#if __NET__
        // init IsEnabled from App.config
        static TraceKeys()
        {
            var prefix = "TraceKeys.";
            var allkeys = ConfigurationManager.AppSettings.AllKeys.Where(x => x.StartsWith(prefix));
            foreach (var key in allkeys)
            {
                var name = key.Substring(prefix.Length);
                var svalue = ConfigurationManager.AppSettings[key];
                var value = string.Compare(svalue, "true", true) == 0
                    || string.Compare(svalue, "on", true) == 0
                    || string.Compare(svalue, "enable", true) == 0
                    || string.Compare(svalue, "enabled", true) == 0
                    || string.Compare(svalue, "1", true) == 0
                    ;
                TraceKeys.Traces[name].IsEnabled = value;
            }
        }
#endif

        /// <summary>
        /// The list of existing traces (i.e. <see cref="TraceKey"/>).
        /// </summary>
        public class TracesProperty : IEnumerable<TraceKey>
        {
            ConcurrentDictionary<string, TraceKey> traces = new ConcurrentDictionary<string, TraceKey>();

            /// <summary>
            /// Gets a <see cref="TraceKey"/> by <paramref name="name"/>.
            /// </summary>
            public TraceKey this[string name]
            {
                get { return GetTrace(name, null); }
            }

            /// <summary>
            /// Gets a <see cref="TraceKey"/> by <paramref name="name"/> and run some code (<paramref name="init"/>) against it.
            /// </summary>
            /// <param name="name">The name of the trace.</param>
            /// <param name="init">Initialization or updating code to run on the <see cref="TraceKey"/>.</param>
            /// <returns>The <see cref="TraceKey"/> with the <paramref name="name"/></returns>
            public TraceKey GetTrace(string name, Action<TraceKey> init)
            {
                lock (traces)
                {
                    TraceKey result;
                    traces.TryGetValue(name, out result);
                    if (result == null)
                    {
                        // REMARK disabled by default! specifically enable for debugging / diagnostic purpose!...
                        traces[name] = result = new TraceKey(name) { IsEnabled = false };
                        if (init != null)
                            init(result);
                    }
                    else
                    {
                        if (init != null)
                            init(result);
                    }
                    return result;
                }
            }

            /// <inheritdoc cref="IEnumerable{T}"/>
            public IEnumerator<TraceKey> GetEnumerator() { return traces.Values.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        /// <summary>
        /// Gets all the traces.
        /// </summary>
        public static TracesProperty Traces { get; } = new TracesProperty();

        /// <summary>
        /// A predefined trace, with <see cref="TraceKey.IsEnabled"/> <c>true</c> by default.
        /// Use that for your main application.
        /// </summary>
        public static TraceKey Application { get; } = Traces.GetTrace(nameof(Application), x => x.IsEnabled = true); /* only one enabled by default */

        /// <summary>
        /// A predefined trace, with <see cref="TraceKey.IsEnabled"/> <c>false</c> by default.
        /// Used by the serialization code to log information.
        /// </summary>
        public static TraceKey Serialization { get; } = Traces[nameof(Serialization)];

        /// <summary>
        /// A predefined trace, with <see cref="TraceKey.IsEnabled"/> <c>false</c> by default.
        /// Used by the <see cref="Galador.Reflection.Registry"/> code to log information.
        /// </summary>
        public static TraceKey Registry { get; } = Traces[nameof(Registry)];
    }
}
