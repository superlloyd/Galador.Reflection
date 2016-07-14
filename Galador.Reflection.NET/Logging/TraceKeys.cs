using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Logging
{
    static partial class TraceKeys
    {
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
    }
}
