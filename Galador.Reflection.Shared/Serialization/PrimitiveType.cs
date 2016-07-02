using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    // REMARK: works with KnownTypes.GetKind() & ObjectContext.WellKnownContext
    // it's why it starts at 1 (null is 0) and Decimal is expected to be last

    public enum PrimitiveType : byte
    {
        None = 0, // not used... just to make sure 0 is reserved
        Object,
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
    }
}
