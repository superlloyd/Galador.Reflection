using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using SRS = System.Runtime.Serialization;

namespace Galador.Reflection.Serialization
{
    public class ObjectData : IConvertible
    {
        internal ObjectData(TypeData type)
        {
            TypeData = type;
        }

        public TypeData TypeData { get; }

        public SRS.SerializationInfo Info { get; internal set; }
        public string ConverterString { get; internal set; }
        public object SurrogateObject { get; internal set; }
        public MemberList<Member> Members { get; } = new MemberList<Member>();

        public IReadOnlyList<object> IList { get; internal set; } = Array.Empty<object>();
        public IReadOnlyList<(object Key, object Value)> IDictionary { get; internal set; } = Array.Empty<(object, object)>();

        #region class Member

        public class Member : IMember
        {
            public string Name { get; internal set; }
            public TypeData Type { get; internal set; }
            public object Value { get; internal set; }
        }

        #endregion

        #region IConvertible, aka help ISerializable cope with breaking change

        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            // TODO
            throw new NotImplementedException("TODO");
        }

        TypeCode IConvertible.GetTypeCode() => TypeCode.Object;

        bool IConvertible.ToBoolean(IFormatProvider provider) => throw new NotSupportedException();

        byte IConvertible.ToByte(IFormatProvider provider) => throw new NotSupportedException();

        char IConvertible.ToChar(IFormatProvider provider) => throw new NotSupportedException();

        DateTime IConvertible.ToDateTime(IFormatProvider provider) => throw new NotSupportedException();

        decimal IConvertible.ToDecimal(IFormatProvider provider) => throw new NotSupportedException();

        double IConvertible.ToDouble(IFormatProvider provider) => throw new NotSupportedException();

        short IConvertible.ToInt16(IFormatProvider provider) => throw new NotSupportedException();

        int IConvertible.ToInt32(IFormatProvider provider) => throw new NotSupportedException();

        long IConvertible.ToInt64(IFormatProvider provider) => throw new NotSupportedException();

        sbyte IConvertible.ToSByte(IFormatProvider provider) => throw new NotSupportedException();

        float IConvertible.ToSingle(IFormatProvider provider) => throw new NotSupportedException();

        string IConvertible.ToString(IFormatProvider provider) => throw new NotSupportedException();

        ushort IConvertible.ToUInt16(IFormatProvider provider) => throw new NotSupportedException();

        uint IConvertible.ToUInt32(IFormatProvider provider) => throw new NotSupportedException();

        ulong IConvertible.ToUInt64(IFormatProvider provider) => throw new NotSupportedException();

        #endregion
    }
}
