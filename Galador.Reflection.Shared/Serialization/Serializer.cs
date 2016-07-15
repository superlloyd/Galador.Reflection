using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Handy helper class to perform typical serialization operation in just one method call.
    /// </summary>
    public static class Serializer
    {
        /// <summary>
        /// Serializes the specified object <paramref name="o"/> as a string.
        /// </summary>
        /// <param name="o">The object to serialize.</param>
        /// <param name="target">The target where the serialized version will be written.</param>
        public static void Serialize(object o, StringBuilder target)
        {
            var pw = new PrimitiveTextWriter(new StringWriter(target, CultureInfo.InvariantCulture));
            var ow = new ObjectWriter(pw);
            ow.Write(o);
        }
        /// <summary>
        /// Deserialize an object from a string.
        /// </summary>
        /// <param name="source">The string to read as a serialized object.</param>
        /// <returns>A newly deserialized object.</returns>
        public static object Deserialize(string source)
        {
            var pr = new PrimitiveTextReader(new StringReader(source));
            var or = new ObjectReader(pr);
            return or.Read();
        }

        /// <summary>
        /// Serializes the specified object <paramref name="o"/> to a stream.
        /// </summary>
        /// <param name="o">The object to serialize.</param>
        /// <param name="target">The target where the serialized version will be written.</param>
        public static void Serialize(object o, Stream target)
        {
            var pw = new PrimitiveBinaryWriter(target);
            var ow = new ObjectWriter(pw);
            ow.Write(o);
        }
        /// <summary>
        /// Deserialize an object from a stream.
        /// </summary>
        /// <param name="source">The stream to read as a serialized object.</param>
        /// <returns>A newly deserialized object.</returns>
        public static object Deserialize(Stream source)
        {
            var pr = new PrimitiveBinaryReader(source);
            var or = new ObjectReader(pr);
            return or.Read();
        }

        /// <summary>
        /// Perform a deep clone operation by serializing then deserializing an object.
        /// </summary>
        /// <typeparam name="T">The type of the object to clone.</typeparam>
        /// <param name="instance">The instance to clone.</param>
        /// <param name="skipMetaData">Whether to set <see cref="ObjectWriter.SkipMetaData"/> to true or not. 
        /// Default value is true. Set this to true to make serialization a little faster.
        /// </param>
        /// <returns>A deep clone of the <paramref name="instance"/>.</returns>
        public static T Clone<T>(T instance, bool skipMetaData = true)
        {
            var ms = new List<object>(256);

            var pw = new TokenPrimitiveWriter(ms);
            var ow = new ObjectWriter(pw)
            {
                SkipMetaData = skipMetaData,
            };

            ow.Write(instance);

            var pr = new TokenPrimitiveReader(ms);
            var or = new ObjectReader(pr)
            {
                SkipMetaData = skipMetaData,
            };

            var result = or.Read();
            return (T)result;
        }

        /// <summary>
        /// Serialize the <paramref name="instance"/> as a string and returns it.
        /// </summary>
        /// <param name="instance">The instance to serialize as a string.</param>
        /// <returns>A string version of the object.</returns>
        public static string ToSerializedString(object instance)
        {
            var sb = new StringBuilder(256);
            Serialize(instance, sb);
            return sb.ToString();
        }
    }
}
