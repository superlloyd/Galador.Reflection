using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Galador.Reflection.Logging
{
    partial class TraceKey
    {
        static partial void WriteText(string s)
        {
            System.Diagnostics.Trace.Write(s);
        }
    }
}
