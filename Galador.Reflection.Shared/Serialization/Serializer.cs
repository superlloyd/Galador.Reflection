using Galador.Reflection.IO;
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
        /// <param name="settings">The serialization settings to use</param>
        public static void Serialize(object o, StringBuilder target, SerializationSettings settings = null)
        {
            var pw = new PrimitiveTextWriter(new StringWriter(target, CultureInfo.InvariantCulture));
            var ow = new ObjectWriter(pw) { Settings = settings };
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
        /// <param name="settings">The serialization settings to use</param>
        public static void Serialize(object o, Stream target, SerializationSettings settings = null)
        {
            var pw = new PrimitiveBinaryWriter(target);
            var ow = new ObjectWriter(pw) { Settings = settings };
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
        /// <param name="settings">The settings to use with the <see cref="ObjectWriter"/></param>
        /// <returns>A deep clone of the <paramref name="instance"/>.</returns>
        public static T Clone<T>(T instance, SerializationSettings settings = null)
        {
            var ms = new List<object>(256);

            var pw = new TokenPrimitiveWriter(ms);
            var ow = new ObjectWriter(pw)
            {
                Settings = settings,
            };

            ow.Write(instance);

            var pr = new TokenPrimitiveReader(ms);
            var or = new ObjectReader(pr);

            var result = or.Read();
            return (T)result;
        }

        /// <summary>
        /// Serialize the <paramref name="instance"/> as a string and returns it.
        /// </summary>
        /// <param name="instance">The instance to serialize as a string.</param>
        /// <param name="settings">The serialization settings to use</param>
        /// <returns>A string version of the object.</returns>
        public static string ToSerializedString(object instance, SerializationSettings settings = null)
        {
            var sb = new StringBuilder(256);
            Serialize(instance, sb, settings);
            return sb.ToString();
        }
    }
}
