using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

#if NET472
using System.Configuration;
#endif

namespace Galador.Reflection
{
    public static class Log
    {
        static Log()
        {
#if NET472
            var prefix = "Log::";
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
                if (value) Enable(name, true);
            }
#endif
        }

        public static void Write(string msg) { Trace.Write(msg); }
        public static void WriteLine(string msg) { Trace.WriteLine(msg); }
        public static void Flush() { Trace.Flush(); }

        public static string Location([CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            return $"{System.IO.Path.GetFileName(path)}:{line}, {method}";
        }
        [Conditional("DEBUG")]
        public static void Cation([CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0) => WriteLine(Location(path, method, line));

        static List<(string key, bool enabled)> enabledList = new List<(string, bool)>();
        static Dictionary<string, bool> enabledCache = new Dictionary<string, bool>();

        public static void EnableNamespace<T>(bool enabled) { Enable(typeof(T).Namespace, enabled); }
        public static void EnableNamespace<T>(T key, bool enabled) { Enable(typeof(T).Namespace, enabled); }
        public static void Enable<T>(bool enabled) { Enable(typeof(T).FullName, enabled); }
        public static void Enable<T>(T key, bool enabled) { Enable(typeof(T).FullName, enabled); }
        public static void Enable(string key, bool enabled)
        {
            if (string.IsNullOrEmpty(key))
                return;
            lock (enabledList)
            {
                enabledCache.Clear();
                int i = enabledList.FindIndex(kv => kv.key == key);
                if (i == -1) enabledList.Add((key, enabled));
                else if (enabledList[i].enabled != enabled) enabledList[i] = (key, enabled);
            }
        }

        public static bool IsEnabled<T>() { return IsEnabled(typeof(T).FullName); }
        public static bool IsEnabled<T>(T key) { return IsEnabled(typeof(T).FullName); }
        public static bool IsEnabled(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;
            lock (enabledList)
            {
                if (!enabledCache.TryGetValue(key, out var result))
                {
                    var matches = from row in enabledList
                                  where key.StartsWith(row.key)
                                  orderby row.key.Length descending
                                  select row.enabled
                                    ;
                    result = matches.FirstOrDefault();
                    enabledCache[key] = result;
                }
                return result;
            }
        }

        public static void Write<T>(string msg) { if (IsEnabled<T>()) Write(msg); }
        public static void Write<T>(T key, string msg) { if (IsEnabled<T>()) Write(msg); }
        public static void Write<T>(T key, string format, params object[] args) { if (IsEnabled<T>()) Write(string.Format(format, args)); }
        public static void Write(string key, string msg) { if (IsEnabled(key)) Write(msg); }
        public static void WriteIf<T>(bool condition, string msg) { if (condition) Write<T>(msg); }
        public static void WriteIf<T>(T key, bool condition, string msg) { if (condition && IsEnabled<T>()) Write(msg); }
        public static void WriteIf<T>(T key, bool condition, string format, params object[] args) { if (IsEnabled<T>()) Write(string.Format(format, args)); }
        public static void WriteIf(string key, bool condition, string msg) { if (condition && IsEnabled(key)) Write(msg); }
        public static void WriteIf(bool condition, string msg) { if (condition) Write(msg); }

        public static void WriteLine<T>(string msg) { if (IsEnabled<T>()) WriteLine(msg); }
        public static void WriteLine<T>(T key, string msg) { if (IsEnabled(key)) WriteLine(msg); }
        public static void WriteLine<T>(T key, string format, params object[] args) { if (IsEnabled<T>()) WriteLine(string.Format(format, args)); }
        public static void WriteLine(string key, string msg) { if (IsEnabled(key)) WriteLine(msg); }
        public static void WriteLineIf<T>(bool condition, string msg) { if (condition && IsEnabled<T>()) WriteLine(msg); }
        public static void WriteLineIf<T>(T key, bool condition, string msg) { if (condition && IsEnabled<T>()) WriteLine(msg); }
        public static void WriteLineIf<T>(T key, bool condition, string format, params object[] args) { if (condition && IsEnabled<T>()) WriteLine(string.Format(format, args)); }
        public static void WriteLineIf(string key, bool condition, string msg) { if (condition && IsEnabled(key)) WriteLine(msg); }
        public static void WriteLineIf(bool condition, string msg) { if (condition) WriteLine(msg); }

        public static string LineHeader([CallerMemberName]string method = "")
        {
            if (method.EndsWith("If"))
                method = method.Substring(0, method.Length - 2);
            return $"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}, {method}: ";
        }

        public static bool IsDebugEnabled { get; set; } = true;
        [Conditional("DEBUG")] public static void Debug(object o) { if (IsDebugEnabled) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void Debug<T>(object o) { if (IsDebugEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void Debug<T>(T key, object o) { if (IsDebugEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void Debug<T>(T key, string format, params object[] args) { if (IsDebugEnabled && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }
        [Conditional("DEBUG")] public static void Debug(string key, object o) { if (IsDebugEnabled && IsEnabled(key)) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void DebugIf(bool condition, object o) { if (IsDebugEnabled && condition) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void DebugIf<T>(bool condition, object o) { if (IsDebugEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void DebugIf<T>(T key, bool condition, object o) { if (IsDebugEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void DebugIf(string key, bool condition, object o) { if (IsDebugEnabled && condition && IsEnabled(key)) WriteLine(LineHeader() + o); }
        [Conditional("DEBUG")] public static void DebugIf<T>(T key, bool condition, string format, params object[] args) { if (IsDebugEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }

        public static bool IsErrorEnabled { get; set; } = true;
        public static void Error(object o) { if (IsErrorEnabled) WriteLine(LineHeader() + o); }
        public static void Error<T>(object o) { if (IsErrorEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Error<T>(T key, object o) { if (IsErrorEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Error<T>(T key, string format, params object[] args) { if (IsErrorEnabled && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }
        public static void Error(string key, object o) { if (IsErrorEnabled && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void ErrorIf(bool condition, object o) { if (IsErrorEnabled && condition) WriteLine(LineHeader() + o); }
        public static void ErrorIf<T>(bool condition, object o) { if (IsErrorEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void ErrorIf<T>(T key, bool condition, object o) { if (IsErrorEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void ErrorIf(string key, bool condition, object o) { if (IsErrorEnabled && condition && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void ErrorIf<T>(T key, bool condition, string format, params object[] args) { if (IsErrorEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }

        public static void Error(Exception e, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void Error(string key, Exception e, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && IsEnabled(key)) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void Error<T>(Exception e, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && IsEnabled<T>()) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void Error<T>(T key, Exception e, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && IsEnabled<T>()) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void ErrorIf(Exception e, bool condition, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && condition) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void ErrorIf(string key, Exception e, bool condition, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && condition && IsEnabled(key)) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void ErrorIf<T>(Exception e, bool condition, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }
        public static void ErrorIf<T>(T key, bool condition, Exception e, [CallerFilePath]string path = null, [CallerMemberName]string method = null, [CallerLineNumber]int line = 0)
        {
            if (IsErrorEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + Location(path, method, line) + ", " + e);
        }

        public static bool IsWarningEnabled { get; set; } = true;
        public static void Warning(object o) { if (IsWarningEnabled) WriteLine(LineHeader() + o); }
        public static void Warning<T>(object o) { if (IsWarningEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Warning<T>(T key, object o) { if (IsWarningEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Warning<T>(T key, string format, params object[] args) { if (IsWarningEnabled && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }
        public static void Warning(string key, object o) { if (IsWarningEnabled && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void WarningIf(bool condition, object o) { if (IsWarningEnabled && condition) WriteLine(LineHeader() + o); }
        public static void WarningIf<T>(bool condition, object o) { if (IsWarningEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void WarningIf<T>(T key, bool condition, object o) { if (IsWarningEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void WarningIf(string key, bool condition, object o) { if (IsWarningEnabled && condition && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void WarningIf<T>(T key, bool condition, string format, params object[] args) { if (IsWarningEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }

        public static bool IsInfoEnabled { get; set; } = true;
        public static void Info(object o) { if (IsInfoEnabled) WriteLine(LineHeader() + o); }
        public static void Info<T>(object o) { if (IsInfoEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Info<T>(T key, object o) { if (IsInfoEnabled && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void Info<T>(T key, string format, params object[] args) { if (IsInfoEnabled && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }
        public static void Info(string key, object o) { if (IsInfoEnabled && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void InfoIf(bool condition, object o) { if (IsInfoEnabled && condition) WriteLine(LineHeader() + o); }
        public static void InfoIf<T>(bool condition, object o) { if (IsInfoEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void InfoIf<T>(T key, bool condition, object o) { if (IsInfoEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + o); }
        public static void InfoIf(string key, bool condition, object o) { if (IsInfoEnabled && condition && IsEnabled(key)) WriteLine(LineHeader() + o); }
        public static void InfoIf<T>(T key, bool condition, string format, params object[] args) { if (IsInfoEnabled && condition && IsEnabled<T>()) WriteLine(LineHeader() + string.Format(format, args)); }
    }
}
