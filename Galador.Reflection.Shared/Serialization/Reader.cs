using Galador.Reflection.Serialization.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class Reader : Context, IDisposable
    {
        readonly IPrimitiveReader input;

        public Reader(IPrimitiveReader input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public void Dispose()
        {
            input.Dispose();
        }
    }
}
