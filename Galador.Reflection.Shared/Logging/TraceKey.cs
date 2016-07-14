using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Galador.Reflection.Logging
{
    /// <summary>
    /// Tracing class. Tracing is done in a thread and won't slow down the app.
    /// </summary>
    public class TraceKey
    {
        internal TraceKey(string name)
        {
            Name = name;
            IsEnabled = true;
            TraceInfo = true;
            TraceWarning = true;
            TraceError = true;
            Header = () => $"{Name}({DateTime.Now:yyyy/MM/dd HH:mm:ss.f}, {Environment.CurrentManagedThreadId:000}): ";
        }

        public Func<string> Header { get; set; }

        public string Name { get; private set; }

        public bool IsEnabled { get; set; }

        public bool TraceInfo { get; set; }
        public bool TraceWarning { get; set; }
        public bool TraceError { get; set; }

        public void Assert(bool condition, string message) { WriteLineIf(!condition, message); }
        public void Assert(bool condition, string format, params object[] args) { WriteLineIf(!condition, format, args); }
        public void WriteIf(bool condition, string msg) { if (condition) Write(msg); }
        public void WriteIf(bool condition, string format, params object[] args) { if (condition) Write(format, args); }
        public void WriteLineIf(bool condition, string msg) { if (condition) WriteLine(msg); }
        public void WriteLineIf(bool condition, string format, params object[] args) { if (condition) WriteLine(format, args); }

        public static void Flush()
        {
#if !__PCL__
            Trace.Flush();
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }

        [Conditional("DEBUG")]
        public void Debug(object o)
        {
            if (!IsEnabled || o == null)
                return;
            WriteLine(GetHeader() + "DEBUG " + o);
        }
        [Conditional("DEBUG")]
        public void Debug(string msg)
        {
            if (!IsEnabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + msg);
        }
        [Conditional("DEBUG")]
        public void Debug(string format, params object[] args)
        {
            if (!IsEnabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + format, args);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string msg)
        {
            if (!condition || !IsEnabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + msg);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string format, params object[] args)
        {
            if (!condition || !IsEnabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + format, args);
        }

        public void Write(object o)
        {
            if (!IsEnabled || o == null)
                return;
#if !__PCL__
            Trace.Write(o);
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }
        public void Write(string msg)
        {
            if (!IsEnabled)
                return;
#if !__PCL__
            Trace.Write(string.Format(msg));
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }
        public void Write(string format, params object[] args)
        {
            if (!IsEnabled)
                return;
#if !__PCL__
            Trace.Write(string.Format(format, args));
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }

        public void WriteLine(object o)
        {
            if (!IsEnabled || o == null)
                return;
#if !__PCL__
            Trace.WriteLine(o);
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }
        public void WriteLine(string msg)
        {
            if (!IsEnabled)
                return;
#if !__PCL__
            Trace.WriteLine(msg);
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }
        public void WriteLine(string format, params object[] args)
        {
            if (!IsEnabled)
                return;
#if !__PCL__
            Trace.WriteLine(string.Format(format, args));
#else
            throw new PlatformNotSupportedException("PCL");
#endif
        }

        string GetHeader()
        {
            if (Header != null)
                return Header();
            return null;
        }

        public const string HeaderError = "ERROR ";
        public const string HeaderWarning = "WARNING ";
        public const string HeaderInfo = "INFO ";

        public void Error(object o)
        {
            if (!IsEnabled || !TraceError || o == null)
                return;
            WriteLine(GetHeader() + HeaderError + o);
        }
        public void Error(string msg)
        {
            if (!IsEnabled || !TraceError)
                return;
            WriteLine(GetHeader() + HeaderError + msg);
        }
        public void Error(string format, params object[] args)
        {
            if (!IsEnabled || !TraceError)
                return;
            WriteLine(GetHeader() + HeaderError + format, args);
        }

        public void Warning(object o)
        {
            if (!IsEnabled || !TraceWarning || o == null)
                return;
            WriteLine(GetHeader() + HeaderWarning + o);
        }
        public void Warning(string msg)
        {
            if (!IsEnabled || !TraceWarning)
                return;
            WriteLine(GetHeader() + HeaderWarning + msg);
        }
        public void Warning(string format, params object[] args)
        {
            if (!IsEnabled || !TraceWarning)
                return;
            WriteLine(GetHeader() + HeaderWarning + format, args);
        }

        public void Information(object o)
        {
            if (!IsEnabled || !TraceInfo || o == null)
                return;
            WriteLine(GetHeader() + HeaderInfo + o);
        }
        public void Information(string msg)
        {
            if (!IsEnabled || !TraceInfo)
                return;
            WriteLine(GetHeader() + HeaderInfo + msg);
        }
        public void Information(string format, params object[] args)
        {
            if (!IsEnabled || !TraceInfo)
                return;
            WriteLine(GetHeader() + HeaderInfo + format, args);
        }
    }
}
