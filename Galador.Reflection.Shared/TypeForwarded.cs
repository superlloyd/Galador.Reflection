#pragma warning disable 1591 // XML Comments
#if !__PCL__
using System.Runtime.CompilerServices;
#endif

#if __PCL__
namespace System
{
    public sealed class DBNull
    {
        public static readonly DBNull Value = new DBNull();
        private DBNull() { }
    }
}
#else
[assembly: TypeForwardedTo(typeof(System.DBNull))]
#endif

#if __PCL__
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
#else
[assembly: TypeForwardedTo(typeof(System.Runtime.Serialization.ISerializable))]
[assembly: TypeForwardedTo(typeof(System.Runtime.Serialization.SerializationInfo))]
#endif


#if __PCL__
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
#else
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverterAttribute))]
[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverter))]
#endif
#pragma warning restore 1591