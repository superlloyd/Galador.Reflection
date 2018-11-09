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
        /// Whether the serialized output contains exhaustive (when <see cref="SkipMemberData"/> is <c>false</c>) or minimal (when <see cref="SkipMemberData"/> is <c>true</c>)
        /// type information. 
        /// <br/>
        /// If <see cref="SkipMemberData"/> is <c>true</c> and the type can not be resolved or there is a version mismatch data would be irrecoverably corrupted.
        /// <br/>
        /// It should NOT be used, unless it is used for in process object deep cloning.
        /// </summary>
        public bool SkipMemberData { get; set; } = false;

        /// <summary>
        /// Whether to ignore <c>ISerializable</c> interface when serializing. Necessary for compatibility with .NET Core.
        /// </summary>
        public bool IgnoreISerializable { get; set; } = false;

        /// <summary>
        /// Whether to ignore <see cref="TypeConverter"/> when serializing. If they prove problematic.
        /// </summary>
        public bool IgnoreTypeConverter { get; set; } = false;


        internal int ToFlags()
        {
            int result = 0;
            result |= SkipMemberData ? 1 << 0 : 0;
            result |= IgnoreISerializable ? 1 << 1 : 0;
            result |= IgnoreTypeConverter ? 1 << 2 : 0;
            return result;
        }
        internal void FromFlags(int l)
        {
            var flags = (int)l;
            SkipMemberData = (flags & (1 << 0)) != 0;
            IgnoreISerializable = (flags & (1 << 1)) != 0;
            IgnoreTypeConverter = (flags & (1 << 2)) != 0;
        }
    }
}
