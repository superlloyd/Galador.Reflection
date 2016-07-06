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
        /// <summary>
        /// A strongly type empty array, i.e. of Length = 0.
        /// </summary>
        public static readonly T[] Array = new T[0];
    }
}
