using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    // REMARK: works with KnownTypes.GetKind() & ObjectContext.WellKnownContext
    // it's why it starts at 1 (null is 0) and Decimal is expected to be last

    /// <summary>
    /// A quick categorization of .NET type
    /// </summary>
    public enum PrimitiveType : byte
    {
        /// <summary>
        /// array, pointers, and unsupported type will be none
        /// </summary>
        None = 0,
        /// <summary>
        /// Anything else that the obvious other type.
        /// </summary>
        Object,
#pragma warning disable 1591 // XML Comments
        String,
        Bytes,
        Guid,
        Bool,
        Char,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double, 
        Decimal,
#pragma warning restore 1591 // XML Comments
    }
}
