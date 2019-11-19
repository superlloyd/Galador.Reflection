using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class Context
    {
        // wellknown serialized objects
        static Context Wellknown()
        {
            var result = new Context();
            // 0 is null
            ulong index = 1;
            result.Register(index++, RuntimeType.GetType(typeof(object)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(string)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Type)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Nullable<>)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(IList)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(IDictionary)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(ICollection<>)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(IDictionary<,>)).TypeData());
            // other well known values, to speed up read-write and reduce stream size
            result.Register(index++, "");
            result.Register(index++, RuntimeType.GetType(typeof(byte[])).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Guid)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(bool)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(char)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(byte)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(sbyte)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(short)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(ushort)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(int)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(uint)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(long)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(ulong)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(float)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(double)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(decimal)).TypeData());
            return result;
        }
        readonly static Context wellknown = Wellknown();

        // wellknown Runtime objects
        public static readonly RuntimeType RObject = RuntimeType.GetType(typeof(object));
        public static readonly RuntimeType RString = RuntimeType.GetType(typeof(string));
        public static readonly RuntimeType RType = RuntimeType.GetType(typeof(Type));

        // context code
        readonly Dictionary<ulong, object> idToObjects = new Dictionary<ulong, object>();
        readonly Dictionary<object, ulong> objectsToIds = new Dictionary<object, ulong>(new ReferenceEqualityComparer());
        ulong seed = 50;

        protected void Register(ulong id, object o)
        {
            if (TryGetObject(id, out var o2))
            {
                if (o == o2)
                    return;
                throw new InvalidOperationException($"{id} already registered");
            }
            // can happen if if there was a surrogate previously but is now gone
            //if (Contains(o))
            //    throw new InvalidOperationException($"{o} already registered");

            idToObjects[id] = o;
            objectsToIds[o] = id;
        }

        protected ulong NewId() => seed++;

        protected object AsMetaData(object obj)
        {
            if (obj is Type)
                return RuntimeType.GetType((Type)obj).TypeData();
            if (obj is RuntimeType)
                return ((RuntimeType)obj).TypeData();
            return obj;
        }
        protected object AsNormalData(object obj)
        {
            if (obj is TypeData)
                return ((TypeData)obj).RuntimeType()?.Type;
            if (obj is RuntimeType)
                return ((RuntimeType)obj).Type;
            return obj;
        }

        public bool Contains(ulong ID)
        {
            if (ID == 0)
                return true;
            if (wellknown != null && wellknown.idToObjects.ContainsKey(ID))
                return true;
            return idToObjects.ContainsKey(ID);
        }

        public bool Contains(object o)
        {
            o = AsMetaData(o);
            if (o == null)
                return true;
            if (wellknown != null && wellknown.objectsToIds.ContainsKey(o))
                return true;
            return objectsToIds.ContainsKey(o);
        }

        public bool TryGetObject(ulong id, out object o)
        {
            if (id == 0)
            {
                o = null;
                return true;
            }

            if (wellknown != null && wellknown.idToObjects.TryGetValue(id, out o))
                return true;
            return idToObjects.TryGetValue(id, out o);
        }

        public bool TryGetId(object o, out ulong id)
        {
            o = AsMetaData(o);
            if (o == null)
            {
                id = 0;
                return true;
            }

            if (wellknown != null && wellknown.objectsToIds.TryGetValue(o, out id))
                return true;
            return objectsToIds.TryGetValue(o, out id);
        }

        public int Count { get { return idToObjects.Count; } }

        public IEnumerable<object> Objects { get { return idToObjects.Values; } }

        public LostData GetLost(object target, bool autoInsert = true)
        {
            if (!lostProperty.TryGetValue(target, out var lost) && autoInsert)
            {
                lost = new LostData(target);
                lostProperty[target] = lost;
            }
            return lost;
        }
        readonly Dictionary<object, LostData> lostProperty = new Dictionary<object, LostData>(new ReferenceEqualityComparer());

        public IEnumerable<LostData> GetLostProperties => lostProperty.Values;

        #region GenerateCSharpCode()

        void RecursiveAdd(RuntimeType type)
        {
            if (type == null || Contains(type))
                return;

            var id = NewId();
            Register(id, type.TypeData());

            RecursiveAdd(type.Element);
            RecursiveAdd(type.BaseType);
            RecursiveAdd(type.Surrogate?.SurrogateType);
            RecursiveAdd(type.Collection1);
            RecursiveAdd(type.Collection2);
            if (type.GenericParameters != null)
                foreach (var item in type.GenericParameters)
                    RecursiveAdd(item);
            foreach (var item in type.Members)
                RecursiveAdd(item.Type);
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all given types.
        /// </summary>
        /// <param name="namespace">The namespace of the generated class.</param>
        /// <param name="types">The types that will be rewritten with only the serialization information.</param>
        /// <returns>A generated C# code file as string.</returns>
        public string GenerateCSharpCode(string @namespace, params Type[] types)
        {
            foreach (var t in types)
            {
                var rt = RuntimeType.GetType(t);
                RecursiveAdd(rt);
            }
            var sb = new StringBuilder(256);
            GenerateCSharpCode(new StringWriter(sb), @namespace);
            return sb.ToString();
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all given types.
        /// </summary>
        /// <param name="namespace">The namespace of the generated class.</param>
        /// <param name="types">The types that will be rewritten with only the serialization information.</param>
        /// <returns>A generated C# code file as string.</returns>
        public string GenerateCSharpCode(string @namespace, params RuntimeType[] types)
        {
            foreach (var t in types)
            {
                RecursiveAdd(t);
            }
            var sb = new StringBuilder(256);
            GenerateCSharpCode(new StringWriter(sb), @namespace);
            return sb.ToString();
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all types registered in this context.
        /// </summary>
        /// <param name="namespace">The namespace of the generated class.</param>
        /// <returns>A generated C# code file as string.</returns>
        public string GenerateCSharpCode(string @namespace)
        {
            var sb = new StringBuilder();
            GenerateCSharpCode(new StringWriter(sb), @namespace);
            return sb.ToString();
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all types registered in this context.
        /// </summary>
        /// <param name="w">The writer to which the generated C# code will be written to.</param>
        /// <param name="namespace">The namespace of the generated class.</param>
        public void GenerateCSharpCode(TextWriter w, string @namespace)
        {
            w.WriteLine("// <auto-generated>");
            w.WriteLine("//     This code was generated by a tool.");
            w.WriteLine("//     But might require manual tweaking.");
            w.WriteLine("// </auto-generated>");
            w.WriteLine();
            w.WriteLine("using System.ComponentModel;");
            w.WriteLine("using System.Collections;");
            w.WriteLine("using System.Collections.Generic;");
            w.WriteLine("using Galador.Reflection.Serialization;");
            w.WriteLine();
            w.Write("namespace "); w.Write(@namespace); w.WriteLine(" {");
            foreach (var item in this.Objects.OfType<TypeData>().Where(x => x.Kind == PrimitiveType.Object && x.IsSupported))
            {
                if (item.IsGeneric && !item.IsGenericTypeDefinition)
                    continue;
                if (item.IsGenericParameter)
                    continue;
                var rtype = item.RuntimeType();
                if (rtype != null && FastType.IsFromMscorlib(rtype.Type))
                    continue;
                w.WriteLine();
                GenerateCSharpCode(w, item);
            }
            w.WriteLine("}");
        }

        void GenerateCSharpCode(TextWriter w, TypeData type)
        {
            if (type.HasConverter)
            {
                w.WriteLine($"\tpublic class {ToTypeName(type)}Converter : TypeConverter {{");
                w.WriteLine("\t\t// TODO ...");
                w.WriteLine("\t}");
                w.WriteLine($"\t[TypeConverter(typeof({ToTypeName(type)}Converter))]");
            }
            string TrimAttribute(string s)
            {
                var suffix = "Attribute";
                if (s.EndsWith(suffix))
                    return s.Substring(0, s.Length - suffix.Length);
                return s;
            }
            if (type.Assembly == null && Guid.TryParse(type.FullName, out var _))
            {
                w.WriteLine($"\t[{TrimAttribute(nameof(SerializationGuidAttribute))}({ToCSharp(type.FullName)})]");
            }
            else
            {
                w.WriteLine($"\t[{TrimAttribute(nameof(SerializationNameAttribute))}({ToCSharp(type.FullName)}, {ToCSharp(type.Assembly)})]");
            }
            if (type.IsEnum)
            {
                w.WriteLine($"\tpublic enum {ToTypeName(type)} : {type.Element}");
                w.WriteLine("\t{");
                w.WriteLine("\t\t// TODO: Values need be entered manually ...");
                w.WriteLine("\t}");
            }
            else
            {
                w.Write("\tpublic ");
                w.Write(type.IsReference ? "class " : "struct ");
                w.Write(ToTypeName(type));
                if (type.IsGenericTypeDefinition)
                {
                    w.Write("<");
                    int N = type.GenericParameters.Count;
                    for (int i = 0; i < N; i++)
                    {
                        if (i > 0)
                            w.Write(",");
                        w.Write("T");
                        w.Write(i + 1);
                    }
                    w.Write(">");
                }
            }
            int interface_count = 0;
            Action addInterface = () =>
            {
                if (interface_count == 0)
                {
                    interface_count = 1;
                    w.Write(" : ");
                }
                else
                {
                    interface_count++;
                    w.Write(", ");
                }
            };
            if (type.BaseType != null && type.BaseType != RObject.TypeData())
            {
                addInterface();
                w.Write(ToTypeName(type.BaseType));
            }
            var tSurrogated = Objects.OfType<TypeData>().Where(x => x.Surrogate == type);
            foreach (var item in tSurrogated)
            {
                addInterface();
                w.Write($"ISurrogate<{ToTypeName(item)}");
                if ((item.GenericParameters?.Count ?? 0) > 0)
                {
                    w.Write("<");
                    int N = item.GenericParameters.Count;
                    for (int i = 0; i < N; i++)
                    {
                        if (i > 0)
                            w.Write(",");
                        w.Write("T");
                        w.Write(i + 1);
                    }
                    w.Write(">");
                }
                w.Write(">");
            }
            if (type.IsISerializable)
            {
                addInterface();
                w.Write("ISerializable");
            }
            switch (type.CollectionType)
            {
                case RuntimeCollectionType.IList:
                    addInterface();
                    w.Write("IList");
                    break;
                case RuntimeCollectionType.ICollectionT:
                    addInterface();
                    w.Write($"ICollection<{ToTypeName(type.Collection1)}>");
                    break;
                case RuntimeCollectionType.IDictionary:
                    addInterface();
                    w.Write("IDictionary");
                    break;
                case RuntimeCollectionType.IDictionaryKV:
                    addInterface();
                    w.Write($"IDictionary<{ToTypeName(type.Collection1)}, {ToTypeName(type.Collection2)}>");
                    break;
            }
            w.WriteLine();
            w.WriteLine("\t{");
            if (type.IsISerializable)
            {
                w.WriteLine($"\t\tpublic {ToTypeName(type)}() {{ }}");
                w.WriteLine($"\t\t{ToTypeName(type)}(SerializationInfo info, StreamingContext context) {{ throw new NotImplementedException(\"TODO\"); }}");
                w.WriteLine("\t\tvoid ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(\"TODO\"); }");
            }
            foreach (var item in tSurrogated)
            {
                w.WriteLine($"\t\tvoid Initialize({ToTypeName(item)} value) {{ throw new NotImplementedException(\"TODO\"); }}");
                w.WriteLine($"\t\t{ToTypeName(item)} Instantiate() {{ throw new NotImplementedException(\"TODO\"); }}");
            }
            foreach (var m in type.Members)
            {
                var mID = ToID(m.Name);
                if (mID != m.Name)
                {
                    w.WriteLine($"\t\t[SerializationMemberName(\"{m.Name}\")]");
                }
                w.Write("\t\tpublic ");
                w.Write(ToTypeName(m.Type));
                w.Write(' ');
                w.Write(mID);
                w.Write(" { get; set; }");
                w.WriteLine();
            }
            w.WriteLine("\t}");
        }
        string ToTypeName(TypeData type)
        {
            var sb = new StringBuilder();
            var sb2 = new StringBuilder();
            ToTypeName(sb, sb2, type);
            return sb.ToString();
        }
        void ToTypeName(StringBuilder sb, StringBuilder sb2, TypeData type)
        {
            string getFriendlyName()
            {
                if (string.IsNullOrWhiteSpace(type.FullName))
                    return "Type_";
                if (Guid.TryParse(type.FullName, out var _))
                    return "Type_";
                var i = type.FullName.LastIndexOfAny(new char[] { '.', '+' });
                if (i + 1 == type.FullName.Length)
                    return "Type_";
                var s = type.FullName.Substring(i + 1);
                sb2.Clear();
                for (i++; i < type.FullName.Length; i++)
                {
                    var c = type.FullName[i];
                    if (c == '`')
                        break;
                    if (char.IsLetterOrDigit(c))
                    {
                        if (sb2.Length == 0 && char.IsDigit(c))
                            sb2.Append('T');
                        sb2.Append(c);
                    }
                }
                if (sb2.Length == 0)
                    return "Type_";
                sb2.Append("_");
                return sb2.ToString();
            }
            if (type.IsEnum)
            {
                sb.Append(getFriendlyName()).Append(objectsToIds[type]);
            }
            else if (type.IsArray)
            {
                ToTypeName(sb, sb2, type.Element);
                for (int i = 1; i < type.ArrayRank; i++)
                    sb.Append(',');
                sb.Append(']');
            }
            else if (type.IsGenericParameter)
            {
                sb.Append('T').Append(type.GenericParameterIndex + 1);
            }
            else if (type.Kind == PrimitiveType.Object)
            {
                var rtype = type.RuntimeType();
                if (type.IsGeneric)
                {
                    if (type.IsGenericTypeDefinition)
                    {
                        if (rtype != null && FastType.IsFromMscorlib(rtype.Type))
                        {
                            var sm = 0;
                            var gm = type.FullName.LastIndexOf('`');
                            if (type.FullName.StartsWith("System.ComponentModel.")) sm = "System.ComponentModel.".Length;
                            else if (type.FullName.StartsWith("System.Collections.Generic.")) sm = "System.Collections.Generic.".Length;
                            else if (type.FullName.StartsWith("System.Collections.")) sm = "System.Collections.".Length;
                            sb.Append(type.FullName.Substring(sm, gm - sm));
                        }
                        else
                        {
                            sb.Append(getFriendlyName()).Append(objectsToIds[type]);
                        }
                    }
                    else
                    {
                        ToTypeName(sb, sb2, type.Element);
                        sb.Append('<');
                        for (int i = 0; i < type.GenericParameters.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(",");
                            ToTypeName(sb, sb2, type.GenericParameters[i]);
                        }
                        sb.Append('>');
                    }
                }
                else
                {
                    if (rtype != null && rtype.Type == typeof(object)) sb.Append("object");
                    else if (rtype != null && FastType.IsFromMscorlib(rtype.Type)) sb.Append(rtype.FullName);
                    else sb.Append(getFriendlyName()).Append(objectsToIds[type]);
                }
            }
            else
            {
                sb.Append(PrimitiveConverter.GetChsarpName(type.Kind));
            }
        }
        string ToID(string s)
        {
            var sb = new StringBuilder(s.Length + 1);
            if (!char.IsLetter(s[0]))
                sb.Append('_');
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();

        }
        string ToCSharp(string s)
        {
            if (s == null)
                return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            // TODO: escape whitespace character and encode unicode
            sb.Append(s);
            sb.Append('"');
            return sb.ToString();
        }

        #endregion
    }
}
