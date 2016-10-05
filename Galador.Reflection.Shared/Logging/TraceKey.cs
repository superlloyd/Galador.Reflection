using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

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
            IsEnabled = true;
            TraceInfo = true;
            TraceWarning = true;
            TraceError = true;
            Header = () => $"{Name}({DateTime.Now:yyyy/MM/dd HH:mm:ss.f}, {Environment.CurrentManagedThreadId:000}): ";
        }

        /// <summary>
        /// Header written at the start of each line. By default output trace's <see cref="Name"/>, date, time and current thread ID.
        /// </summary>
        public Func<string> Header { get; set; }

        /// <summary>
        /// Name of the trace
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Whether the trace is enabled or not. Disabled trace do not write anything.
        /// </summary>
        /// <remarks>
        /// All <see cref="TraceKey.IsEnabled"/> state can be initialized in th <c>App.config</c> file on the full .NET framework.
        /// With the key <c>"TraceKeys." + trace.Name</c>, and value <c>true, false, on, off</c>
        /// </remarks>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Whether to disable the <see cref="Information(object)"/> methods and overrides.
        /// </summary>
        public bool TraceInfo { get; set; }

        /// <summary>
        /// Whether to disable the <see cref="Warning(object)"/> methods and overrides.
        /// </summary>
        public bool TraceWarning { get; set; }

        /// <summary>
        /// Whether to disable the <see cref="Error(object)"/> method and override.
        /// </summary>
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

        /// <summary>
        /// Additional header for <see cref="Error(object)"/> methods.
        /// </summary>
        public const string HeaderError = "ERROR ";

        /// <summary>
        /// Additional header for <see cref="Warning(object)"/> methods
        /// </summary>
        public const string HeaderWarning = "WARNING ";

        /// <summary>
        /// Additional header for <see cref="Information(object)"/> methods.
        /// </summary>
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
#pragma warning restore 1591 // code comments