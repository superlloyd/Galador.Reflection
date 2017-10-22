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
                TraceKeys.Traces.Enable(name, value);
            }
        }
#endif

        /// <summary>
        /// Gets all the traces already in use.
        /// </summary>
        public static TracesProperty Traces { get; } = new TracesProperty();

        /// <summary>
        /// The list of existing traces (i.e. <see cref="TraceKey"/>).
        /// </summary>
        public class TracesProperty : IEnumerable<TraceKey>
        {
            ConcurrentDictionary<string, TraceKey> traces = new ConcurrentDictionary<string, TraceKey>();
            List<(string name, bool enable)> enabledList = new List<(string name, bool enable)>();


            /// <summary>
            /// Gets a <see cref="TraceKey"/> by <paramref name="type"/>.
            /// </summary>
            public TraceKey this[Type type] { get { return this[type.FullName]; } }

            /// <summary>
            /// Gets a <see cref="TraceKey"/> by <paramref name="name"/>.
            /// </summary>
            public TraceKey this[string name]
            {
                get
                {
                    lock (traces)
                    {
                        TraceKey result;
                        traces.TryGetValue(name, out result);
                        if (result == null)
                            traces[name] = result = new TraceKey(name);
                        return result;
                    }
                }
            }

            /// <inheritdoc cref="IEnumerable{T}"/>
            public IEnumerator<TraceKey> GetEnumerator() { return traces.Values.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

            /// <summary>
            /// Enable (or disable, depending on <paramref name="enable"/>) by default all traces which start with <paramref name="prefix"/>.
            /// </summary>
            public void Enable(string prefix, bool enable)
            {
                lock (enabledList)
                {
                    int i = enabledList.FindIndex(x => x.name == prefix);
                    if (i > -1)
                        enabledList[i] = (prefix, enable);
                    else
                        enabledList.Add((prefix, enable));
                }
            }

            /// <summary>
            /// Whether or not traces in argument namespace are enabled.
            /// </summary>
            /// <param name="name">namespace to test</param>
            /// <returns>whether argument namespace is enabled or not</returns>
            public bool IsEnabled(string name)
            {
                lock (enabledList)
                {
                    var matches = from row in enabledList
                                    where name.StartsWith(row.name)
                                    orderby name.Length descending
                                    select row.enable
                                    ;
                    return matches.FirstOrDefault();
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="TraceKey"/> by <paramref name="name"/> and set its enabled state.
        /// </summary>
        /// <param name="name">The name of the trace.</param>
        /// <param name="enable">whether or not it's enabled.</param>
        /// <returns>The <see cref="TraceKey"/> with the <paramref name="name"/></returns>
        public static TraceKey GetTrace(string name, bool enable = false)
        {
            var result = Traces[name];
            if (enable)
                Traces.Enable(name, true);
            return result;
        }

        /// <summary>
        /// Whether to disable the <see cref="TraceKey.Debug(object)"/> methods and overrides.
        /// </summary>
        public static bool TraceDebug { get; set; } = true;

        /// <summary>
        /// Whether to disable the <see cref="TraceKey.Information(object)"/> methods and overrides.
        /// </summary>
        public static bool TraceInfo { get; set; } = true;

        /// <summary>
        /// Whether to disable the <see cref="TraceKey.Warning(object)"/> methods and overrides.
        /// </summary>
        public static bool TraceWarning { get; set; } = true;

        /// <summary>
        /// Whether to disable the <see cref="TraceKey.Error(object)"/> method and override.
        /// </summary>
        public static bool TraceError { get; set; } = true;

        /// <summary>
        /// Get a trace for a class. Its key will be the type full name, i.e. including namespace.
        /// </summary>
        public static TraceKey Get<T>() { return Traces[typeof(T)]; }

        /// <summary>
        /// Get a trace for an object. Its key will be the object's type full name, i.e. including namespace.
        /// </summary>
        public static TraceKey Get(object o) { return Traces[o.GetType()]; }

        /// <summary>
        /// A predefined trace, which is enabled by default.
        /// Use that for your main application.
        /// </summary>
        public static TraceKey Application { get; } = GetTrace(nameof(Application), true); /* only one enabled by default */
    }
}
