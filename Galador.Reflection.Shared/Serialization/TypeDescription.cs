using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;
using Galador.Reflection.Utils;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// <see cref="TypeConverter"/> for <see cref="TypeDescription"/>
    /// </summary>
    public class TypeDescriptionConverter : System.ComponentModel.TypeConverter
    {
#if !__PCL__
        /// <summary>
        /// Determines whether this instance [can convert from] the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="sourceType">Type of the source.</param>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }
        /// <summary>
        /// Determines whether this instance [can convert to] the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="destinationType">Type of the destination.</param>
        /// <returns></returns>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }
        /// <summary>
        /// Converts from.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = (string)value;
            return new TypeDescription(s);
        }
        /// <summary>
        /// Converts to.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="value">The value.</param>
        /// <param name="destinationType">Type of the destination.</param>
        /// <returns></returns>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var ts = (TypeDescription)value;
            return ts.ToString();
        }
#endif
    }

    /// <summary>
    /// This class help transform type to string description and vice version. 
    /// The string format is very similar to standard .NET type string, except the assembly name is just the name, without version of hash.
    /// </summary>
    /// <remarks>This class is a left over of previous iteration and not use anywhere by the serializer currently</remarks>
    [System.ComponentModel.TypeConverter(typeof(TypeDescriptionConverter))]
    public sealed class TypeDescription
    {
        /// <summary>
        /// FullName of the represented type.
        /// </summary>
        public string Fullname { get; private set; }
        /// <summary>
        /// Assembly name for the assembly declaring that type.
        /// </summary>
        public string AssemblyName { get; private set; }
        /// <summary>
        /// How many 'stars' for pointer type.
        /// </summary>
        public byte PointerCount { get; private set; }

        /// <summary>
        /// Type argument for generic type.
        /// </summary>
        public IReadOnlyList<TypeDescription> TypeArguments { get { return mTypeArguments; } }
        List<TypeDescription> mTypeArguments = new List<TypeDescription>();

        /// <summary>
        /// Array ranks for array type.
        /// </summary>
        public IReadOnlyList<byte> ArrayRanks { get { return mArrayRanks; } }
        List<byte> mArrayRanks = new List<byte>();


        /// <summary>
        /// Compare this to another <see cref="TypeDescription"/>
        /// </summary>
        /// <returns>Whether <param name="obj"/> is a <see cref="TypeDescription"/> of the same type, or not.</returns>
        public override bool Equals(object obj)
        {
            var o = obj as TypeDescription;
            if (o == null
                || o.Fullname != Fullname
                || o.AssemblyName != AssemblyName)
                return false;
            if (o.TypeArguments.Count != TypeArguments.Count)
                return false;
            for (int i = 0; i < TypeArguments.Count; i++)
                if (!TypeArguments[i].Equals(o.TypeArguments[i]))
                    return false;
            if (o.ArrayRanks.Count != ArrayRanks.Count)
                return false;
            for (int i = 0; i < ArrayRanks.Count; i++)
                if (ArrayRanks[i] != o.ArrayRanks[i])
                    return false;
            return true;
        }
        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            int val = Fullname.GetHashCode();
            if (AssemblyName != null)
                val ^= AssemblyName.GetHashCode();
            foreach (var item in TypeArguments)
                val ^= item.GetHashCode();
            val ^= ArrayRanks.Select(x => (int)x).Sum();
            return base.GetHashCode();
        }

        /// <summary>
        /// Create a <see cref="TypeDescription"/> for <param name="type"></param>
        /// </summary>
        public TypeDescription(Type type)
        {
#if __PCL__
            throw new PlatformNotSupportedException("PCL");
#else
            while (type.IsArray)
            {
                var rank = type.GetArrayRank();
                mArrayRanks.Insert(0, (byte)rank);
                type = type.GetElementType();
            }
            while (type.IsPointer)
            {
                PointerCount++;
                type = type.GetElementType();
            }
            if (type.GetTypeInfo().IsGenericType && !type.GetTypeInfo().IsGenericTypeDefinition)
            {
                foreach (var arg in type.GetTypeInfo().GetGenericArguments())
                {
                    var sub = new TypeDescription(arg);
                    mTypeArguments.Add(sub);
                }
                type = type.GetGenericTypeDefinition();
            }
            Fullname = type.FullName;
            var ass = type.GetTypeInfo().Assembly;
            if (ass != FastType.MSCORLIB)
            {
                AssemblyName = ass.GetName().Name;
            }
#endif
        }

        /// <summary>
        /// Parse a string as a TypeDescription
        /// </summary>
        /// <param name="s"></param>
        public TypeDescription(string s) { Parse(s, 0); }

        TypeDescription() { }
        int Parse(string s, int index)
        {
            Func<int, int> next = (start) =>
            {
                for (int i = start; i < s.Length; i++)
                {
                    var c = s[i];
                    if (c == ',' || c == '[' || c == ']' || c == '*')
                        return i;
                }
                return s.Length;
            };

            var i2 = next(index);
            Fullname = s.Substring(index, i2 - index).Trim();
            if (i2 >= s.Length || s[i2] == ']')
                return i2;
            if (s[i2] == ',')
                goto labelAssembly;
            if (s[i2] == '*')
                goto labelPointer;
            var i3 = next(i2 + 1);
            if (s[i3] == ',' || s[i3] == ']')
                goto labelArray;
            if (i2 < s.Length && s[i2] == '[')
            {
                i2 = i3;
                while (i2 < s.Length && s[i2] != ']')
                {
                    if (s[i2] == ',')
                        i2 = next(i2 + 1);
                    if (i2 >= s.Length || s[i2] != '[')
                        return i2;
                    var sub = new TypeDescription();
                    mTypeArguments.Add(sub);
                    i2 = sub.Parse(s, i2 + 1);
                    if (i2 >= s.Length || s[i2] != ']')
                        return i2;
                    i2 = next(i2 + 1);
                }
                i2 = next(i2 + 1);
            }
            labelPointer:
            while (i2 < s.Length && s[i2] == '*')
            {
                PointerCount++;
                i2++;
            }
            labelArray:
            if (i2 < s.Length && s[i2] == '[')
            {
                byte rank = 1;
                do
                {
                    i2 = next(i2 + 1);
                    if (i2 < s.Length && s[i2] == ',')
                        checked { rank++; };
                }
                while (i2 < s.Length && s[i2] != ']');
                mArrayRanks.Add(rank);
                i2++;
                if (i2 < s.Length && s[i2] == '[')
                    goto labelArray;
            }
            labelAssembly:
            if (i2 < s.Length && s[i2] == ',')
            {
                i3 = next(i2 + 1);
                AssemblyName = s.Substring(i2 + 1, i3 - i2 - 1).Trim();
                i2 = i3;
            }
            return i2;
        }

        /// <summary>
        /// Attempt to create the type that corresponds to that description.
        /// </summary>
        public Type Resolve()
        {
#if __PCL__
            throw new PlatformNotSupportedException("PCL");
#else
            var type = KnownTypes.GetType(Fullname, AssemblyName);
            if (type == null)
                return null;
            if (TypeArguments.Count > 0)
            {
                if (!type.GetTypeInfo().IsGenericTypeDefinition || type.GetTypeInfo().GetGenericArguments().Length != TypeArguments.Count)
                    return null;
                var args = TypeArguments.Select(x => x.Resolve()).ToArray();
                if (args.Any(x => x == null))
                    return null;
                type = type.MakeGenericType(args);
            }
            if (PointerCount > 0)
            {
                int pc = PointerCount;
                while (pc-- > 0)
                    type = type.MakePointerType();
            }
            foreach (var rank in ArrayRanks)
            {
                if (rank == 1) type = type.MakeArrayType();
                else type = type.MakeArrayType(rank);
            }
            return type;
#endif
        }
        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }
        void ToString(StringBuilder sb)
        {
            sb.Append(Fullname);
            if (TypeArguments.Count > 0)
            {
                sb.Append('[');
                for (int i = 0; i < TypeArguments.Count; i++)
                {
                    var arg = TypeArguments[i];
                    if (i > 0)
                        sb.Append(',');
                    sb.Append('[');
                    arg.ToString(sb);
                    sb.Append(']');

                }
                sb.Append(']');
            }
            if (PointerCount > 0)
                sb.Append('*', PointerCount);
            foreach (var rank in ArrayRanks)
            {
                sb.Append('[');
                for (int i = 0; i < rank - 1; i++)
                    sb.Append(',');
                sb.Append(']');
            }
            if (AssemblyName != null)
                sb.Append(",").Append(AssemblyName);
        }
    }
}
