using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public static class Serializer
    {
        public static void Serialize(object o, StringBuilder target)
        {
            var pw = new PrimitiveTextWriter(new StringWriter(target, CultureInfo.InvariantCulture));
            var ow = new ObjectWriter(pw);
            ow.Write(o);
        }
        public static object Deserialize(string source)
        {
            var pr = new PrimitiveTextReader(new StringReader(source));
            var or = new ObjectReader(pr);
            return or.Read();
        }
        public static void Serialize(object o, Stream target)
        {
            var pw = new PrimitiveBinaryWriter(target);
            var ow = new ObjectWriter(pw);
            ow.Write(o);
        }
        public static object Deserialize(Stream source)
        {
            var pr = new PrimitiveBinaryReader(source);
            var or = new ObjectReader(pr);
            return or.Read();
        }
        public static T Clone<T>(T instance, bool skipMetaData = true)
        {
            var ms = new MemoryStream(256);

            var pw = new PrimitiveBinaryWriter(ms);
            var ow = new ObjectWriter(pw)
            {
                SkipMetaData = skipMetaData,
            };

            ow.Write(instance);

            ms.Position = 0;
            var pr = new PrimitiveBinaryReader(ms);
            var or = new ObjectReader(pr)
            {
                SkipMetaData = skipMetaData,
            };

            var result = or.Read();
            return (T)result;
        }
        public static string ToSerializedString(object instance)
        {
            var sb = new StringBuilder(256);
            Serialize(instance, sb);
            return sb.ToString();
        }
    }
}
