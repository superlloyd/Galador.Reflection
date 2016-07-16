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
    public static class TraceKeys
    {
#if __NET__

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

        public class TracesProperty : IEnumerable<TraceKey>
        {
            ConcurrentDictionary<string, TraceKey> traces = new ConcurrentDictionary<string, TraceKey>();
            public TraceKey this[string key]
            {
                get { return GetTrace(key, null); }
            }

            public TraceKey GetTrace(string key, Action<TraceKey> init)
            {
                lock (traces)
                {
                    TraceKey result;
                    traces.TryGetValue(key, out result);
                    if (result == null)
                    {
                        // REMARK disabled by default! specifically enable for debugging / diagnostic purpose!...
                        traces[key] = result = new TraceKey(key) { IsEnabled = false };
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

        public static TracesProperty Traces { get; } = new TracesProperty();

        public static TraceKey Application { get; } = Traces.GetTrace(nameof(Application), x => x.IsEnabled = true); /* only one enabled by default */
        public static TraceKey Serialization { get; } = Traces[nameof(Serialization)];
        public static TraceKey Registry { get; } = Traces[nameof(Registry)];
    }
}
