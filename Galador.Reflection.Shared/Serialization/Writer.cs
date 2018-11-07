using Galador.Reflection.Serialization.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class Writer : Context, IDisposable
    {
        readonly IPrimitiveWriter output;

        public Writer(IPrimitiveWriter output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void Dispose()
        {
            output.Dispose();
        }
    }
}
