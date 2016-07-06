using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// A class that will contain all references written by the <see cref="ObjectWriter"/> or read by the <see cref="ObjectReader"/>
    /// Could be use for reference purpose.
    /// </summary>
    public class ObjectContext
    {
        internal static readonly ObjectContext WellKnownContext;

        static ObjectContext()
        {
            WellKnownContext = new ObjectContext();
            // 0 <==> null
            WellKnownContext.Register(1, ReflectType.RObject);
            WellKnownContext.Register(2, ReflectType.RString);
            WellKnownContext.Register(3, ReflectType.RType);
            WellKnownContext.Register(4, ReflectType.RReflectType);
            WellKnownContext.Register(5, "");
        }

        #region serialization methods: TryGetObject() Contains() TryGetId() NewId() Register()

        Dictionary<ulong, object> idToObjects = new Dictionary<ulong, object>();
        Dictionary<object, ulong> objectsToIds = new Dictionary<object, ulong>();
        ulong seed = (ulong)(WellKnownContext != null ? WellKnownContext.Count : 0) + 1;

        /// <summary>
        /// When reading object, check whether they are already known with that method
        /// </summary>
        public bool TryGetObject(ulong id, out object o)
        {
            if (id == 0)
            {
                o = null;
                return true;
            }
            if (this != WellKnownContext && WellKnownContext.TryGetObject(id, out o))
                return true;
            return idToObjects.TryGetValue(id, out o);
        }

        /// <summary>
        /// When writing object, check if they are already registered with this method
        /// </summary>
        public bool TryGetId(object o, out ulong id)
        {
            if (o == null)
            {
                id = 0;
                return true;
            }
            if (this != WellKnownContext && WellKnownContext.TryGetId(o, out id))
                return true;
            return objectsToIds.TryGetValue(o, out id);
        }

        /// <summary>
        /// When writing object, check if they are already registered with this method
        /// </summary>
        public bool Contains(object o)
        {
            if (o == null)
                return true;
            return (WellKnownContext != null && WellKnownContext.objectsToIds.ContainsKey(o))
                || objectsToIds.ContainsKey(o);
        }

        /// <summary>
        /// Whether an object has been registered with this ID
        /// </summary>
        public bool Contains(ulong ID)
        {
            if (ID == 0)
                return true;
            return (WellKnownContext != null && WellKnownContext.idToObjects.ContainsKey(ID))
                || idToObjects.ContainsKey(ID);
        }

        /// <summary>
        /// Get a new ID to use for an object
        /// </summary>
        internal ulong NewId()
        {
            do { seed++; }
            while (Contains(seed));
            return seed;
        }

        /// <summary>
        /// Register unknown object with that method
        /// </summary>
        internal void Register(ulong id, object o)
        {
            if (this != WellKnownContext && WellKnownContext.Contains(id))
                throw new InvalidOperationException($"ID({id}) already in use");
            if (idToObjects.ContainsKey(id))
                throw new InvalidOperationException($"ID({id}) already in use");
            if (id == 0)
                throw new InvalidOperationException($"null(0) already registered");
            if (objectsToIds.ContainsKey(o))
                throw new InvalidOperationException($"Object({o}) already registered");
            idToObjects[id] = o;
            if (o != null)
                objectsToIds[o] = id;
        }

        /// <summary>
        /// Due to its recursive nature, some hashcode might have been incorrect at registration
        /// data, redo that table
        /// </summary>
        internal void UpdateObjectsToIDs()
        {
            objectsToIds.Clear();
            foreach (var kv in idToObjects)
            {
                if (kv.Value == null)
                    continue;
                objectsToIds[kv.Value] = kv.Key;
            }
        }

        #endregion

        #region info: Count, IDs, Objects this[]

        /// <summary>
        /// Number of object that have been register with this context.
        /// </summary>
        public int Count { get { return idToObjects.Count; } }

        /// <summary>
        /// All the IDs.
        /// </summary>
        public IEnumerable<ulong> IDs { get { return idToObjects.Keys; } }

        /// <summary>
        /// All the objects
        /// </summary>
        public IEnumerable<object> Objects { get { return idToObjects.Values; } }

        /// <summary>
        /// Gets the <see cref="System.Object"/> with the specified ID.
        /// </summary>
        /// <param name="ID">The ID of the object.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">If no object was registered for that ID.</exception>
        public object this[ulong ID]
        {
            get
            {
                object o;
                if (ID == 0ul)
                    return null;
                if (this != WellKnownContext && WellKnownContext.idToObjects.TryGetValue(ID, out o))
                    return o;
                if (idToObjects.TryGetValue(ID, out o))
                    return o;
                throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region GenerateCSharpCode()

        void RecursizeAdd(ReflectType type)
        {
            if (Contains(type))
                return;
            var id = NewId();
            Register(id, type);

            RecursizeAdd(type.Element);
            RecursizeAdd(type.Surrogate);
            RecursizeAdd(type.BaseType);
            RecursizeAdd(type.Collection1);
            RecursizeAdd(type.Collection2);
            foreach (var item in type.GenericArguments)
                RecursizeAdd(item);
            foreach (var item in type.Members)
                RecursizeAdd(item.Type);
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all given types.
        /// </summary>
        /// <param name="namespace">The namespace of the generated class.</param>
        /// <param name="types">The types that will be rewritten with only the serialization information.</param>
        /// <returns>A generated C# code file as string.</returns>
        public static string GenerateCSharpCode(string @namespace, params Type[] types)
        {
            var ctxt = new ObjectContext();
            foreach (var t in types)
            {
                var rt = ReflectType.GetType(t);
                ctxt.RecursizeAdd(rt);
            }
            var sb = new StringBuilder(256);
            ctxt.GenerateCSharpCode(new StringWriter(sb), @namespace);
            return sb.ToString();
        }

        /// <summary>
        /// Generates the C# class that can be used to deserialize all given types.
        /// </summary>
        /// <param name="namespace">The namespace of the generated class.</param>
        /// <param name="types">The types that will be rewritten with only the serialization information.</param>
        /// <returns>A generated C# code file as string.</returns>
        public static string GenerateCSharpCode(string @namespace, params ReflectType[] types)
        {
            var ctxt = new ObjectContext();
            foreach (var t in types)
            {
                ctxt.RecursizeAdd(t);
            }
            var sb = new StringBuilder(256);
            ctxt.GenerateCSharpCode(new StringWriter(sb), @namespace);
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
#if !__PCL__
            w.WriteLine("// <auto-generated>");
            w.WriteLine("//     This code was generated by a tool.");
            w.WriteLine("//     But might require manual tweaking.");
            w.WriteLine("// </auto-generated>");
            w.WriteLine();
            w.WriteLine("using System.ComponentModel;");
            w.WriteLine("using System.Collections;");
            w.WriteLine("using System.Collections.Generic;");
            w.WriteLine();
            w.Write("namespace "); w.Write(@namespace); w.WriteLine(" {");
            foreach (var item in this.Objects.OfType<ReflectType>().Where(x => x.Kind == PrimitiveType.Object && !x.IsIgnored))
            {
                if (item.IsGeneric && !item.IsGenericTypeDefinition)
                    continue;
                if (item.IsSurrogateType)
                    continue;
                if (item.IsMscorlib())
                    continue;
                w.WriteLine();
                GenerateCSharpCode(w, item);
            }
            w.WriteLine("}");
#endif
        }

#if !__PCL__
        void GenerateCSharpCode(TextWriter w, ReflectType type, ReflectType isSurrogateFor = null)
        {
            if (type.HasConverter && type.IsGeneric) // TODO: IsGeneric only... because not sure...
            {
                w.WriteLine($"\tpublic class {ToCSharp(type.TypeName)}Converter : TypeConverter {{");
                w.WriteLine("\t\t// TODO ...");
                w.WriteLine("\t}");
                w.WriteLine($"\t[TypeConverter({ToCSharp(type.TypeName)}Converter)]");
            }
            w.WriteLine($"\t[{nameof(SerializationNameAttribute)}({ToCSharp(type.TypeName)}, {ToCSharp(type.AssemblyName)})]");
            if (type.IsEnum)
            {
                w.WriteLine($"\tpublic enum Type{objectsToIds[type]} : {type.Element}");
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
                    int N = type.GenericArguments.Count;
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
            if (type.BaseType != null && type.BaseType != ReflectType.RObject)
            {
                addInterface();
                w.Write(ToTypeName(type.BaseType));
            }
            if (isSurrogateFor != null)
            {
                addInterface();
                w.WriteLine($"ISurrogate<{ToTypeName(isSurrogateFor)}>");
            }
            if (type.IsISerializable)
            {
                addInterface();
                w.Write("ISerializable");
            }
            switch (type.CollectionType)
            {
                case ReflectCollectionType.IList:
                    addInterface();
                    w.Write("Ilist");
                    break;
                case ReflectCollectionType.ICollectionT:
                    addInterface();
                    w.Write($"ICollection<{ToTypeName(type.Collection1)}>");
                    break;
                case ReflectCollectionType.IDictionary:
                    addInterface();
                    w.Write("IDictionary");
                    break;
                case ReflectCollectionType.IDictionaryKV:
                    addInterface();
                    w.Write($"IDictionary<{ToTypeName(type.Collection1)}, {ToTypeName(type.Collection2)}>");
                    break;
            }
            w.WriteLine();
            w.WriteLine("\t{");
            w.WriteLine($"\t\tpublic Type{objectsToIds[type]}() {{ }}");
            if (type.IsISerializable)
            {
                w.WriteLine($"\t\tType{objectsToIds[type]}(SerializationInfo info, StreamingContext context) {{ throw new NotImplementedException(\"TODO\"); }}");
                w.WriteLine("\t\tvoid ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(\"TODO\"); }");
            }
            if (isSurrogateFor != null)
            {
                w.WriteLine($"\t\tvoid Initialize({ToTypeName(isSurrogateFor)} value) {{ throw new NotImplementedException(\"TODO\"); }}");
                w.WriteLine($"\t\t{ToTypeName(isSurrogateFor)} Instantiate() {{ throw new NotImplementedException(\"TODO\"); }}");
            }
            foreach (var m in type.Members)
            {
                w.Write("\t\tpublic ");
                w.Write(ToTypeName(m.Type));
                w.Write(' ');
                w.Write(m.Name);
                w.Write(" { get; set; }");
                w.WriteLine();
            }
            w.WriteLine("\t}");
            if (type.Surrogate != null)
                GenerateCSharpCode(w, type.Surrogate, type);
        }
        string ToTypeName(ReflectType type)
        {
            var sb = new StringBuilder();
            ToTypeName(sb, type);
            return sb.ToString();
        }
        void ToTypeName(StringBuilder sb, ReflectType type)
        {
            if (type.IsArray)
            {
                ToTypeName(sb, type.Element);
                for (int i = 1; i < type.ArrayRank; i++)
                    sb.Append(',');
                sb.Append(']');
            }
            else if (type.IsPointer)
            {
                ToTypeName(sb, type.Element);
                sb.Append('*');
            }
            else if (type.IsGenericParameter)
            {
                sb.Append('T').Append(type.GenericParameterIndex + 1);
            }
            else if (type.Kind == PrimitiveType.Object)
            {
                if (type.IsGeneric)
                {
                    if (type.IsGenericTypeDefinition)
                    {
                        if (type.IsMscorlib())
                        {
                            var sm = 0;
                            var gm = type.TypeName.LastIndexOf('`');
                            if (type.TypeName.StartsWith("System.ComponentModel.")) sm = "System.ComponentModel.".Length;
                            else if (type.TypeName.StartsWith("System.Collections.Generic.")) sm = "System.Collections.Generic.".Length;
                            else if (type.TypeName.StartsWith("System.Collections.")) sm = "System.Collections.".Length;
                            sb.Append(type.TypeName.Substring(sm, gm - sm));
                        }
                        else
                        {
                            sb.Append("Type").Append(objectsToIds[type]);
                        }
                    }
                    else
                    {
                        ToTypeName(sb, type.Element);
                        sb.Append('<');
                        for (int i = 0; i < type.GenericArguments.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(",");
                            ToTypeName(sb, type.GenericArguments[i]);
                        }
                        sb.Append('>');
                    }
                }
                else
                {
                    if (type.Type == typeof(object)) sb.Append("object");
                    else if (type.IsMscorlib()) sb.Append(type.TypeName);
                    else sb.Append("Type").Append(objectsToIds[type]);
                }
            }
            else
            {
                sb.Append(type.ToString());
            }
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
#endif

        #endregion
    }
}
