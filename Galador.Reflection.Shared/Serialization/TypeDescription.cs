using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace Galador.Reflection.Serialization
{
    public class TypeDescriptionConverter : System.ComponentModel.TypeConverter
    {
#if !__PCL__
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var s = (string)value;
            return new TypeDescription(s);
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var ts = (TypeDescription)value;
            return ts.ToString();
        }
#endif
    }

    /// <summary>
    /// Bonus class to help translate Type to string and vice versa. Also it is compatible 
    /// with <code>Type.GetType()</code> and use it under the hood. It is also not used by the serialization code.
    /// </summary>
    [System.ComponentModel.TypeConverter(typeof(TypeDescriptionConverter))]
    public sealed class TypeDescription
    {
        public string Fullname { get; private set; }
        public string AssemblyName { get; private set; }
        public byte PointerCount { get; private set; }

        public IReadOnlyList<TypeDescription> TypeArguments { get { return mTypeArguments; } }
        List<TypeDescription> mTypeArguments = new List<TypeDescription>();

        public IReadOnlyList<byte> ArrayRanks { get { return mArrayRanks; } }
        List<byte> mArrayRanks = new List<byte>();


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
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    var sub = new TypeDescription(arg);
                    mTypeArguments.Add(sub);
                }
                type = type.GetGenericTypeDefinition();
            }
            Fullname = type.FullName;
            var ass = type.Assembly;
            if (ass != typeof(object).Assembly)
            {
                AssemblyName = ass.GetName().Name;
            }
#endif
        }

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
        public Type Resolve()
        {
#if __PCL__
            throw new PlatformNotSupportedException("PCL");
#else
            var elementName = AssemblyName != null ? $"{Fullname},{AssemblyName}" : Fullname;
            var type = Type.GetType(elementName);
            if (type == null)
                return null;
            if (TypeArguments.Count > 0)
            {
                if (!type.GetTypeInfo().IsGenericTypeDefinition || type.GetGenericArguments().Length != TypeArguments.Count)
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
