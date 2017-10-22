using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 1591 // code comments
namespace Galador.Reflection.Logging
{
    /// <summary>
    /// Thin multiplatform wrapper around <c>System.Diagnostics.Trace</c>.
    /// All its methods, but <see cref="Write(string)"/> and <see cref="WriteLine(string)"/>, 
    /// will start the line with a <see cref="Header"/>.
    /// </summary>
    public sealed class TraceKey
    {
        internal TraceKey(string name)
        {
            Name = name;
            Header = (level) => $"{Name}({DateTime.Now:yyyy/MM/dd HH:mm:ss.f}, {Environment.CurrentManagedThreadId:000}, {level}): ";
        }

        /// <summary>
        /// Header written at the start of each line. By default output trace's <see cref="Name"/>, date, time and current thread ID.
        /// </summary>
        public Func<string, string> Header { get; set; }

        /// <summary>
        /// Name of the trace
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Will flush the traces now
        /// </summary>
        public static void Flush()
        {
#if !__PCL__
            Trace.Flush();
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }

        public void Write(string msg)
        {
            if (!IsEnabled || string.IsNullOrEmpty(msg))
                return;
#if !__PCL__
            Trace.Write(string.Format(msg));
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }
        public void WriteLine(string msg)
        {
            if (!IsEnabled)
                return;
#if !__PCL__
            Trace.Write(string.Format(msg));
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }

        string GetHeader(string level) { return Header?.Invoke(level); }
        const string HeaderDebug = "DEBUG";
        const string HeaderError = "ERROR";
        const string HeaderWarning = "WARNING";
        const string HeaderInfo = "INFO";
        internal bool IsEnabled { get; set; }
        bool TraceDebug { get { return IsEnabled && TraceKeys.TraceDebug; } }
        bool TraceInfo { get { return IsEnabled && TraceKeys.TraceInfo; } }
        bool TraceWarning { get { return IsEnabled && TraceKeys.TraceInfo; } }
        bool TraceError { get { return IsEnabled && TraceKeys.TraceInfo; } }

        public void Write(object o)
        {
            if (!IsEnabled || o == null)
                return;
            Write(o?.ToString());
        }

        public void Write(string format, params object[] args)
        {
            if (!IsEnabled || string.IsNullOrEmpty(format))
                return;
            Write(string.Format(format, args));
        }

        public void WriteLine(object o)
        {
            if (!IsEnabled)
                return;
            WriteLine(o?.ToString());
        }
        public void WriteLine(string format, params object[] args)
        {
            if (!IsEnabled)
                return;
            WriteLine(string.Format(format, args));
        }

        public void Assert(bool condition, string message) { WriteLineIf(!condition, message); }
        public void Assert(bool condition, string format, params object[] args) { WriteLineIf(!condition, format, args); }
        public void WriteIf(bool condition, string msg) { if (condition) Write(msg); }
        public void WriteIf(bool condition, string format, params object[] args) { if (condition) Write(format, args); }
        public void WriteLineIf(bool condition, string msg) { if (condition) WriteLine(msg); }
        public void WriteLineIf(bool condition, string format, params object[] args) { if (condition) WriteLine(format, args); }

        [Conditional("DEBUG")]
        public void Debug(object o)
        {
            if (!TraceDebug || o == null)
                return;
            WriteLine(GetHeader(HeaderDebug) + o);
        }
        [Conditional("DEBUG")]
        public void Debug(string msg)
        {
            if (!TraceDebug || string.IsNullOrWhiteSpace(msg))
                return;
            WriteLine(GetHeader(HeaderDebug) + msg);
        }
        [Conditional("DEBUG")]
        public void Debug(string format, params object[] args)
        {
            if (!TraceDebug || string.IsNullOrWhiteSpace(format))
                return;
            WriteLine(GetHeader(HeaderDebug) + format, args);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string msg)
        {
            if (!condition || !TraceDebug || string.IsNullOrWhiteSpace(msg))
                return;
            WriteLine(GetHeader(HeaderDebug) + msg);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string format, params object[] args)
        {
            if (!condition || !TraceDebug || string.IsNullOrWhiteSpace(format))
                return;
            WriteLine(GetHeader(HeaderDebug) + format, args);
        }

        public void Error(object o)
        {
            if (!TraceError || o == null)
                return;
            WriteLine(GetHeader(HeaderError) + o);
        }
        public void Error(string msg)
        {
            if (!TraceError || string.IsNullOrWhiteSpace(msg))
                return;
            WriteLine(GetHeader(HeaderError) + msg);
        }
        public void Error(string format, params object[] args)
        {
            if (!TraceError || string.IsNullOrWhiteSpace(format))
                return;
            WriteLine(GetHeader(HeaderError) + format, args);
        }

        public void Warning(object o)
        {
            if (!TraceWarning || o == null)
                return;
            WriteLine(GetHeader(HeaderWarning) + o);
        }
        public void Warning(string msg)
        {
            if (!TraceWarning || string.IsNullOrWhiteSpace(msg))
                return;
            WriteLine(GetHeader(HeaderWarning) + msg);
        }
        public void Warning(string format, params object[] args)
        {
            if (!TraceWarning || string.IsNullOrWhiteSpace(format))
                return;
            WriteLine(GetHeader(HeaderWarning) + format, args);
        }

        public void Information(object o)
        {
            if (!TraceInfo || o == null)
                return;
            WriteLine(GetHeader(HeaderInfo) + o);
        }
        public void Information(string msg)
        {
            if (!TraceInfo || string.IsNullOrWhiteSpace(msg))
                return;
            WriteLine(GetHeader(HeaderInfo) + msg);
        }
        public void Information(string format, params object[] args)
        {
            if (!TraceInfo || string.IsNullOrWhiteSpace(format))
                return;
            WriteLine(GetHeader(HeaderInfo) + format, args);
        }
    }
}
#pragma warning restore 1591 // code comments