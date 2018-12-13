using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Settings for the <see cref="ObjectWriter"/> that will affect serialization
    /// </summary>
    public class SerializationSettings
    {
        /// <summary>
        /// Whether to ignore <c>ISerializable</c> interface when serializing. Necessary for compatibility with .NET Core.
        /// </summary>
        public bool IgnoreISerializable { get; set; } = true;

        /// <summary>
        /// Whether to ignore <see cref="TypeConverter"/> when serializing. If they prove problematic.
        /// </summary>
        public bool IgnoreTypeConverter { get; set; } = true;


        internal int ToFlags()
        {
            int result = 0;
            result |= IgnoreISerializable ? 1 << 1 : 0;
            result |= IgnoreTypeConverter ? 1 << 2 : 0;
            return result;
        }
        internal void FromFlags(int l)
        {
            var flags = (int)l;
            IgnoreISerializable = (flags & (1 << 1)) != 0;
            IgnoreTypeConverter = (flags & (1 << 2)) != 0;
        }
    }
}
