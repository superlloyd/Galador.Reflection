using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Utils
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
        /// Anything else that the obvious other values.
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

    /// <summary>
    /// Help convert between .NET type and the associated <see cref="PrimitiveType"/>
    /// </summary>
    public static class PrimitiveConverter
    {
        /// <summary>
        /// Whther this is a known structure or not.
        /// </summary>
        public static bool IsStruct(PrimitiveType type)
        {
            switch (type)
            {
                default:
                case PrimitiveType.None:
                case PrimitiveType.Object:
                case PrimitiveType.String:
                case PrimitiveType.Bytes:
                    return false;
                case PrimitiveType.Guid:
                case PrimitiveType.Bool:
                case PrimitiveType.Char:
                case PrimitiveType.Byte:
                case PrimitiveType.SByte:
                case PrimitiveType.Int16:
                case PrimitiveType.UInt16:
                case PrimitiveType.Int32:
                case PrimitiveType.UInt32:
                case PrimitiveType.Int64:
                case PrimitiveType.UInt64:
                case PrimitiveType.Single:
                case PrimitiveType.Double:
                case PrimitiveType.Decimal:
                    return true;
            }
        }

        /// <summary>
        /// Return the .NET <see cref="Type"/> for a <see cref="PrimitiveType"/>, or null in ambiguous case (<see cref="PrimitiveType.Object"/>).
        /// </summary>
        public static Type GetType(PrimitiveType type)
        {
            switch (type)
            {
                default:
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    return null;
                case PrimitiveType.String: return typeof(string);
                case PrimitiveType.Bytes: return typeof(byte[]);
                case PrimitiveType.Guid: return typeof(Guid);
                case PrimitiveType.Bool: return typeof(bool);
                case PrimitiveType.Char: return typeof(char);
                case PrimitiveType.Byte: return typeof(byte);
                case PrimitiveType.SByte: return typeof(sbyte);
                case PrimitiveType.Int16: return typeof(short);
                case PrimitiveType.UInt16: return typeof(ushort);
                case PrimitiveType.Int32: return typeof(int);
                case PrimitiveType.UInt32: return typeof(uint);
                case PrimitiveType.Int64: return typeof(long);
                case PrimitiveType.UInt64: return typeof(ulong);
                case PrimitiveType.Single: return typeof(float);
                case PrimitiveType.Double: return typeof(double);
                case PrimitiveType.Decimal: return typeof(decimal);
            }
        }

        /// <summary>
        /// Return the <see cref="PrimitiveType"/> for .NET <see cref="Type"/>.
        /// </summary>
        public static PrimitiveType GetPrimitiveType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type == typeof(string)) return PrimitiveType.String;
            if (type == typeof(byte[])) return PrimitiveType.Bytes;
            if (type == typeof(Guid)) return PrimitiveType.Guid;
            if (type == typeof(bool)) return PrimitiveType.Bool;
            if (type == typeof(char)) return PrimitiveType.Char;
            if (type == typeof(byte)) return PrimitiveType.Byte;
            if (type == typeof(sbyte)) return PrimitiveType.SByte;
            if (type == typeof(short)) return PrimitiveType.Int16;
            if (type == typeof(ushort)) return PrimitiveType.UInt16;
            if (type == typeof(int)) return PrimitiveType.Int32;
            if (type == typeof(uint)) return PrimitiveType.UInt32;
            if (type == typeof(long)) return PrimitiveType.Int64;
            if (type == typeof(ulong)) return PrimitiveType.UInt64;
            if (type == typeof(float)) return PrimitiveType.Single;
            if (type == typeof(double)) return PrimitiveType.Double;
            if (type == typeof(decimal)) return PrimitiveType.Decimal;
            return PrimitiveType.Object;
        }
    }
}
