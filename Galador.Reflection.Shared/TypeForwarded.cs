#pragma warning disable 1591 // XML Comments

#if !__PCL__ && !__NETCORE__

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

[assembly: TypeForwardedTo(typeof(System.ICloneable))]
[assembly: TypeForwardedTo(typeof(System.DBNull))]
[assembly: TypeForwardedTo(typeof(System.Runtime.Serialization.ISerializable))]
[assembly: TypeForwardedTo(typeof(System.Runtime.Serialization.SerializationInfo))]
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverterAttribute))]
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverter))]

#endif

#if __NETCORE__

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

[assembly: TypeForwardedTo(typeof(System.DBNull))]
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverterAttribute))]
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverter))]

namespace System
{
    public interface ICloneable
    {
        object Clone();
    }
}
namespace System.Runtime.Serialization
{
    public interface ISerializable
    {
        void GetObjectData(SerializationInfo info, StreamingContext context);
    }
    public sealed class SerializationInfo
    {
    }
}

#endif

#if __PCL__

namespace System
{
    public interface ICloneable
    {
        object Clone();
    }
    public sealed class DBNull
    {
        public static readonly DBNull Value = new DBNull();
        private DBNull() { }
    }
}
namespace System.ComponentModel
{
    public sealed class TypeConverterAttribute : System.Attribute
    {
        public TypeConverterAttribute() { throw new PlatformNotSupportedException("PCL"); }
        public TypeConverterAttribute(Type type) { throw new PlatformNotSupportedException("PCL"); }
        public TypeConverterAttribute(string typeName) { throw new PlatformNotSupportedException("PCL"); }
        public string ConverterTypeName { get { throw new PlatformNotSupportedException("PCL"); } }
    }
    public class TypeConverter
    {
    }
}

namespace System.Runtime.Serialization
{
    public interface ISerializable
    {
        void GetObjectData(SerializationInfo info, StreamingContext context);
    }
    public sealed class SerializationInfo
    {
    }
}

#endif

#pragma warning restore 1591