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
    partial class TraceKey
    {

        public TraceKey()
        {
            Enabled = true;
            TraceInfo = true;
            TraceWarning = true;
            TraceError = true;
            Header = () => $"{DateTime.Now:yyyy/MM/dd HH:mm:ss.f} {Environment.CurrentManagedThreadId:000} ";
        }

        public Func<string> Header { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public bool TraceInfo { get; set; }
        public bool TraceWarning { get; set; }
        public bool TraceError { get; set; }

        public void Assert(bool condition, string message) { WriteLineIf(!condition, message); }
        public void Assert(bool condition, string format, params object[] args) { WriteLineIf(!condition, format, args); }
        public void WriteIf(bool condition, string msg) { if (condition) Write(msg); }
        public void WriteIf(bool condition, string format, params object[] args) { if (condition) Write(format, args); }
        public void WriteLineIf(bool condition, string msg) { if (condition) WriteLine(msg); }
        public void WriteLineIf(bool condition, string format, params object[] args) { if (condition) WriteLine(format, args); }

        #region GlobalTraceSink

        //static AsyncQueue writeQueue = new AsyncQueue("TraceKey.LogQueue");

        static void GlobalTraceSink(string s)
        {
            //writeQueue.Queue(() => 
            {
                try
                {
                    WriteText(s);
                }
                catch (Exception)
                {
#if DEBUG
                    if (global::System.Diagnostics.Debugger.IsAttached)
                        global::System.Diagnostics.Debugger.Break();
#endif
                }
            }
            //);
        }
        static partial void WriteText(string s);

        public static void Flush()
        {
            //writeQueue.Flush();
        }

        #endregion

        [Conditional("DEBUG")]
        public void Debug(object o)
        {
            if (!Enabled || o == null)
                return;
            WriteLine(GetHeader() + "DEBUG " + o);
        }
        [Conditional("DEBUG")]
        public void Debug(string msg)
        {
            if (!Enabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + msg);
        }
        [Conditional("DEBUG")]
        public void Debug(string format, params object[] args)
        {
            if (!Enabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + format, args);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string msg)
        {
            if (!condition || !Enabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + msg);
        }
        [Conditional("DEBUG")]
        public void DebugIf(bool condition, string format, params object[] args)
        {
            if (!condition || !Enabled)
                return;
            WriteLine(GetHeader() + "DEBUG " + format, args);
        }

        public void Write(object o)
        {
            if (!Enabled || o == null)
                return;
            if (o != null)
                GlobalTraceSink(o.ToString());
        }
        public void Write(string msg)
        {
            if (!Enabled)
                return;
            GlobalTraceSink(msg);
        }
        public void Write(string format, params object[] args)
        {
            if (!Enabled)
                return;
            GlobalTraceSink(string.Format(format, args));
        }

        public void WriteLine(object o)
        {
            if (!Enabled || o == null)
                return;
            if (o != null)
                GlobalTraceSink(o.ToString());
            GlobalTraceSink("\r\n");
        }
        public void WriteLine(string msg)
        {
            if (!Enabled)
                return;
            GlobalTraceSink(msg);
            GlobalTraceSink("\r\n");
        }
        public void WriteLine(string format, params object[] args)
        {
            if (!Enabled)
                return;
            GlobalTraceSink(string.Format(format, args));
            GlobalTraceSink("\r\n");
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
            if (!Enabled || !TraceError || o == null)
                return;
            WriteLine(GetHeader() + HeaderError + o);
        }
        public void Error(string msg)
        {
            if (!Enabled || !TraceError)
                return;
            WriteLine(GetHeader() + HeaderError + msg);
        }
        public void Error(string format, params object[] args)
        {
            if (!Enabled || !TraceError)
                return;
            WriteLine(GetHeader() + HeaderError + format, args);
        }

        public void Warning(object o)
        {
            if (!Enabled || !TraceWarning || o == null)
                return;
            WriteLine(GetHeader() + HeaderWarning + o);
        }
        public void Warning(string msg)
        {
            if (!Enabled || !TraceWarning)
                return;
            WriteLine(GetHeader() + HeaderWarning + msg);
        }
        public void Warning(string format, params object[] args)
        {
            if (!Enabled || !TraceWarning)
                return;
            WriteLine(GetHeader() + HeaderWarning + format, args);
        }

        public void Information(object o)
        {
            if (!Enabled || !TraceInfo || o == null)
                return;
            WriteLine(GetHeader() + HeaderInfo + o);
        }
        public void Information(string msg)
        {
            if (!Enabled || !TraceInfo)
                return;
            WriteLine(GetHeader() + HeaderInfo + msg);
        }
        public void Information(string format, params object[] args)
        {
            if (!Enabled || !TraceInfo)
                return;
            WriteLine(GetHeader() + HeaderInfo + format, args);
        }
    }
}
