using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Provides immutable empty list instances.
    /// </summary>
    public static class Empty<T>
    {
        public static readonly T[] Array = new T[0];
    }
}
