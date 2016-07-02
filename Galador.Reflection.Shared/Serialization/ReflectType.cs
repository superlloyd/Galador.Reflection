using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO;

namespace Galador.Reflection.Serialization
{
    public enum ReflectCollectionType : byte
    {
        None,
        IList,
        ICollectionT,
        IDictionary,
        IDictionaryKV,
    }
    // TODO add support for DataContract, DataMember, IgnoreMember
    public sealed class ReflectType
    {
        #region GetType()

        public static ReflectType GetType(object o)
        {
            if (o == null)
                return RObject;
            if (o is Type)
                return RType;
            if (o is ReflectType)
                return RReflectType;
            if (o is Missing)
                return ((Missing)o).Type;
            return GetType(o.GetType());
        }

        public static ReflectType GetType(Type type)
        {
            if (type == null)
                return null;
            lock (sReflectCache)
            {
                ReflectType result;
                if (!sReflectCache.TryGetValue(type, out result))
                {
                    result = new ReflectType();
                    sReflectCache[type] = result;
                    result.Initialize(type);
                }
                return result;
            }
        }
        static Dictionary<Type, ReflectType> sReflectCache = new Dictionary<System.Type, ReflectType>();

        #endregion

        // this must be first
        static readonly SerializationSettingsAttribute DefaultSettings = new SerializationSettingsAttribute();

        public static readonly ReflectType RObject = GetType(typeof(object));
        public static readonly ReflectType RReflectType = GetType(typeof(ReflectType));
        public static readonly ReflectType RString = GetType(typeof(string));
        public static readonly ReflectType RType = GetType(typeof(Type));
        public static readonly ReflectType RNullable = GetType(typeof(Nullable<>));

        // flags
        public PrimitiveType Kind { get; private set; } = PrimitiveType.None;
        public ReflectCollectionType CollectionType { get; private set; } = ReflectCollectionType.None;
        public bool IsPointer { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsGeneric { get; private set; }
        public bool IsGenericTypeDefinition { get; private set; }
        public bool IsGenericParameter { get; private set; }
        public bool IsEnum { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsIgnored { get; private set; }
        public bool IsFinal { get; private set; } = true;
        public bool HasSurrogate { get; private set; }
        public bool HasConverter { get; private set; }
        public bool IsISerializable { get; private set; }
        public bool IsReference { get; private set; }
        public bool IsDefaultSave { get; private set; }

        // non flags
        public string TypeName { get; private set; }
        public string AssemblyName { get; private set; }
        public int ArrayRank { get; private set; }
        public int GenericParameterIndex { get; private set; }
        public Type Type { get; private set; }
        public ReflectType Surrogate { get; private set; }
        public ReflectType Element { get; private set; }
        public ReflectType Collection1 { get; private set; }
        public ReflectType Collection2 { get; private set; }
        public IReadOnlyList<ReflectType> GenericArguments { get; private set; } = Empty<ReflectType>.Array;
        public MemberList Members { get; } = new MemberList();

        internal MethodInfo listWrite, listRead;

        #region ctors() Initialize()

        internal ReflectType() { }

        void Initialize(Type type)
        {
            Type = type;
            var ti = type.GetTypeInfo();
            if (type.IsArray)
            {
                IsReference = true;
                if (type == typeof(byte[]))
                {
                    Kind = PrimitiveType.Bytes;
                }
                else
                {
                    IsArray = true;
                    ArrayRank = type.GetArrayRank();
                    Element = GetType(type.GetElementType());
                }
            }
            else if (type.IsPointer)
            {
                IsIgnored = true;
                IsPointer = true;
                Element = GetType(type.GetElementType());
            }
            else if (type.IsGenericParameter)
            {
#if __PCL__
                throw new NotSupportedException("PCL");
#else
                IsGenericParameter = true;
                var pargs = type.DeclaringType.GetGenericArguments();
                for (int i = 0; i < pargs.Length; i++)
                    if (pargs[i] == type)
                    {
                        GenericParameterIndex = i;
                        break;
                    }
#endif
            }
            else
            {
                Kind = KnownTypes.GetKind(type);
                if (Kind == PrimitiveType.String)
                {
                    IsReference = true;
                }
                else if (Kind == PrimitiveType.Object)
                {
                    if (ti.IsGenericType)
                    {
                        IsGeneric = true;
                        if (ti.IsGenericTypeDefinition)
                        {
                            IsGenericTypeDefinition = true;
                        }
                        else
                        {
#if __PCL__
                        throw new NotSupportedException("PCL");
#else
                            Element = GetType(ti.GetGenericTypeDefinition());
                            IsNullable = Element == RNullable;
                            GenericArguments = ti.GetGenericArguments().Select(x => GetType(x)).ToArray();
                            IsIgnored = GenericArguments.Any(x => x.IsIgnored);
#endif
                        }
                    }
                    if (!IsGeneric || IsGenericTypeDefinition)
                    {
                        var att = ti.GetCustomAttribute<SerializationNameAttribute>();
                        if (att == null)
                        {
                            TypeName = type.FullName;
                            if (ti.Assembly != typeof(object).GetTypeInfo().Assembly)
                                AssemblyName = ti.Assembly.GetName().Name;
                        }
                        else
                        {
                            TypeName = att.TypeName;
                            AssemblyName = att.AssemblyName;
                        }
                    }
                    if (ti.IsEnum)
                    {
                        Element = GetType(ti.GetEnumUnderlyingType());
                        IsEnum = true;
                    }
                    else if (typeof(Delegate).IsBaseClass(type) || type == typeof(IntPtr))
                    {
                        IsIgnored = true;
                        IsReference = ti.IsClass;
                    }
                    else
                    {
                        IsReference = ti.IsClass || ti.IsInterface;
                        IsFinal = !ti.IsClass || ti.IsSealed;
                        IsDefaultSave = true;
                        Type tSurrogate;
                        if (KnownTypes.TryGetSurrogateType(type, out tSurrogate))
                        {
                            Surrogate = GetType(tSurrogate);
                            HasSurrogate = true;
                        }
                        else if (typeof(ISerializable).IsBaseClass(type))
                        {
                            IsISerializable = true;
                        }
                        else if (GetTypeConverter() != null)
                        {
                            HasConverter = true;
                        }
                        else
                        {
                            var attr = ti.GetCustomAttribute<SerializationSettingsAttribute>() ?? DefaultSettings;
                            foreach (var pi in ti.GetRuntimeFields())
                            {
                                var pType = GetType(pi.FieldType);
                                if (pType.IsIgnored)
                                    continue;
                                if (pi.IsStatic)
                                    continue;
                                if (pi.GetCustomAttribute<NotSerializedAttribute>() != null)
                                    continue;
                                bool inc = pi.IsPublic ? attr.IncludePublicFields : attr.IncludePrivateFields;
                                if (!inc && pi.GetCustomAttribute<SerializedAttribute>() != null)
                                    inc = true;
                                if (!inc)
                                    continue;
                                var m = new Member
                                {
                                    Name = pi.Name,
                                    Type = pType,
                                    fInfo = pi,
                                };
                                Members.Add(m);
                            }
                            foreach (var pi in ti.GetRuntimeProperties())
                            {
                                var pType = GetType(pi.PropertyType);
                                if (pType.IsIgnored)
                                    continue;
                                if (pi.GetMethod == null || pi.GetMethod.IsStatic || pi.GetMethod.GetParameters().Length != 0)
                                    continue;
                                if (pi.GetCustomAttribute<NotSerializedAttribute>() != null)
                                    continue;
                                bool inc = pi.GetMethod.IsPublic ? attr.IncludePublicProperties : attr.IncludePrivateProperties;
                                if (!inc && pi.GetCustomAttribute<SerializedAttribute>() != null)
                                    inc = true;
                                if (!inc)
                                    continue;
                                var m = new Member
                                {
                                    Name = pi.Name,
                                    Type = pType,
                                    pInfo = pi,
                                };
                                Members.Add(m);
                            }
                            var interfaces = type.GetTypeHierarchy().Where(x => x.GetTypeInfo().IsInterface).Distinct().ToList();
                            var itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary<,>)).FirstOrDefault();
                            if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary)).FirstOrDefault();
                            if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(ICollection<>)).FirstOrDefault();
                            if (itype == null) itype = interfaces.Where(t => t.GetTypeInfo().IsGenericType && t.GetTypeInfo().GetGenericTypeDefinition() == typeof(IList)).FirstOrDefault();
                            if (itype != null)
                            {
                                var iti = itype.GetTypeInfo();
                                if (iti.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                                {
                                    CollectionType = ReflectCollectionType.IDictionaryKV;
                                    if (iti.IsGenericTypeDefinition)
                                    {
                                        Collection1 = GetType(iti.GenericTypeParameters[0]);
                                        Collection2 = GetType(iti.GenericTypeParameters[1]);
                                    }
                                    else
                                    {
                                        Collection1 = GetType(iti.GenericTypeArguments[0]);
                                        Collection2 = GetType(iti.GenericTypeArguments[1]);
                                    }
                                }
                                else if (itype.GetTypeInfo().GetGenericTypeDefinition() == typeof(IDictionary))
                                {
                                    CollectionType = ReflectCollectionType.IDictionary;
                                }
                                else if (itype.GetTypeInfo().GetGenericTypeDefinition() == typeof(ICollection<>))
                                {
                                    CollectionType = ReflectCollectionType.ICollectionT;
                                    if (iti.IsGenericTypeDefinition)
                                    {
                                        Collection1 = GetType(iti.GenericTypeParameters[0]);
                                    }
                                    else
                                    {
                                        Collection1 = GetType(iti.GenericTypeArguments[0]);
                                    }
                                }
                                else
                                {
                                    CollectionType = ReflectCollectionType.IList;
                                }
                            }
                        }
                    }
                }
            }
            InitHashCode();
        }

        #endregion

        #region GetHashCode() Equals() ToString()

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            var o = obj as ReflectType;
            if (o == null)
                return false;
            if (Type != null && o.Type != null)
                return Type == o.Type;
            if (FlagsToInt() != o.FlagsToInt())
                return false;
            if (IsArray)
            {
                if (ArrayRank != o.ArrayRank)
                    return false;
                if (!Element.Equals(o.Element))
                    return false;
            }
            else if (IsPointer)
            {
                if (!Element.Equals(o.Element))
                    return false;
            }
            else if (IsGenericParameter)
            {
                if (GenericParameterIndex != o.GenericParameterIndex)
                    return false;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                        if (!Element.Equals(o.Element))
                            return false;
                    if (Element.GenericArguments.Count != o.GenericArguments.Count)
                        return false;
                    for (int i = 0; i < GenericArguments.Count; i++)
                        if (!Element.GenericArguments[i].Equals(o.GenericArguments[i]))
                            return false;
                }
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    if (TypeName != o.TypeName)
                        return false;
                    if (AssemblyName != o.AssemblyName)
                        return false;
                }
            }
            return true;
        }

        public override int GetHashCode() { return hashcode; }
        int hashcode;
        void InitHashCode()
        {
            hashcode = FlagsToInt();
            if (IsArray)
            {
                hashcode ^= ArrayRank;
                hashcode ^= Element.GetHashCode();
            }
            else if (IsPointer)
            {
                hashcode ^= Element.GetHashCode();
            }
            else if (IsGenericParameter)
            {
                hashcode ^= GenericParameterIndex;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                        hashcode ^= Element.GetHashCode();
                    foreach (var item in GenericArguments)
                        hashcode ^= item.GetHashCode();
                }
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    hashcode ^= TypeName.GetHashCode();
                    if (AssemblyName != null)
                        hashcode ^= AssemblyName.GetHashCode();
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            ToString(sb);
            return sb.ToString();
        }
        void ToString(StringBuilder sb)
        {
            string assembly;
            ToString(sb, out assembly);
            if (assembly != null)
                sb.Append(',').Append(assembly);
        }
        void ToString(StringBuilder sb, out string assembly)
        {
            if (IsArray)
            {
                Element.ToString(sb, out assembly);
                sb.Append('[');
                for (int i = 1; i < ArrayRank; i++)
                    sb.Append(',');
                sb.Append(']');
            }
            else if (IsPointer)
            {
                Element.ToString(sb, out assembly);
                sb.Append('*');
            }
            else if (IsGenericParameter)
            {
                sb.Append('T').Append(GenericParameterIndex);
                assembly = null;
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (IsGeneric)
                {
                    if (IsGenericTypeDefinition)
                    {
                        assembly = AssemblyName;
                        sb.Append(TypeName);
                    }
                    else
                    {
                        Element.ToString(sb, out assembly);
                        sb.Append('[');
                        for (int i = 0; i < GenericArguments.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(",");
                            sb.Append('[');
                            GenericArguments[i].ToString(sb);
                            sb.Append(']');
                        }
                        sb.Append(']');
                    }
                }
                else
                {
                    assembly = AssemblyName;
                    sb.Append(TypeName);
                }
            }
            else
            {
                assembly = null;
                switch (Kind)
                {
                    default:
                    case PrimitiveType.None:
                    case PrimitiveType.Object:
                        sb.Append("????");
                        break;
                    case PrimitiveType.String:
                        sb.Append("string");
                        break;
                    case PrimitiveType.Bytes:
                        sb.Append("byte[]");
                        break;
                    case PrimitiveType.Guid:
                        sb.Append("Guid");
                        break;
                    case PrimitiveType.Bool:
                        sb.Append("bool");
                        break;
                    case PrimitiveType.Char:
                        sb.Append("char");
                        break;
                    case PrimitiveType.Byte:
                        sb.Append("byte");
                        break;
                    case PrimitiveType.SByte:
                        sb.Append("sbyte");
                        break;
                    case PrimitiveType.Int16:
                        sb.Append("short");
                        break;
                    case PrimitiveType.UInt16:
                        sb.Append("ushort");
                        break;
                    case PrimitiveType.Int32:
                        sb.Append("int");
                        break;
                    case PrimitiveType.UInt32:
                        sb.Append("uint");
                        break;
                    case PrimitiveType.Int64:
                        sb.Append("long");
                        break;
                    case PrimitiveType.UInt64:
                        sb.Append("ulong");
                        break;
                    case PrimitiveType.Single:
                        sb.Append("float");
                        break;
                    case PrimitiveType.Double:
                        sb.Append("double");
                        break;
                    case PrimitiveType.Decimal:
                        sb.Append("decimal");
                        break;
                }
            }
        }

        #endregion

        #region class Member MemberList

        public class Member
        {
            public string Name { get; internal set; }
            public ReflectType Type { get; internal set; }
            internal PropertyInfo pInfo;
            internal FieldInfo fInfo;

            public object GetValue(object instance)
            {
                if (pInfo != null && pInfo.GetMethod != null)
                    return pInfo.GetValue(instance);
                if (fInfo != null)
                    return fInfo.GetValue(instance);
                return null;
            }
            public void SetValue(object instance, object value)
            {
                if (pInfo != null && pInfo.SetMethod != null && pInfo.PropertyType.IsInstanceOf(value))
                    pInfo.SetValue(instance, value);
                else if (fInfo != null && fInfo.FieldType.IsInstanceOf(value))
                    fInfo.SetValue(instance, value);
            }
        }

        public class MemberList : IReadOnlyList<Member>, IReadOnlyDictionary<string, Member>
        {
            List<Member> list = new List<Member>();
            Dictionary<string, Member> dict = new Dictionary<string, Member>();

            internal MemberList()
            {
            }
            internal void Add(Member m)
            {
                if (dict.ContainsKey(m.Name))
                    throw new ArgumentException();
                list.Add(m);
                dict[m.Name] = m;
            }

            public Member this[string key]
            {
                get
                {
                    Member m;
                    dict.TryGetValue(key, out m);
                    return m;
                }
            }
            public Member this[int index] { get { return list[index]; } }
            public int Count { get { return list.Count; } }
            public IEnumerable<string> Keys { get { return dict.Keys; } }
            public IEnumerable<Member> Values { get { return list; } }

            public bool ContainsKey(string key) { return dict.ContainsKey(key); }
            public IEnumerator<Member> GetEnumerator() { return list.GetEnumerator(); }
            bool IReadOnlyDictionary<string, Member>.TryGetValue(string key, out Member value) { return dict.TryGetValue(key, out value); }

            IEnumerator<KeyValuePair<string, Member>> IEnumerable<KeyValuePair<string, Member>>.GetEnumerator() { return dict.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
        }

        #endregion

        #region FlagsToInt() IntToFlags()

        int FlagsToInt()
        {
            int result = ((int)Kind) | ((int)CollectionType << 8);
            if (IsPointer) result |= 1 << 16;
            if (IsArray) result |= 1 << 17;
            if (IsGeneric) result |= 1 << 18;
            if (IsGenericTypeDefinition) result |= 1 << 19;
            if (IsGenericParameter) result |= 1 << 20;
            if (IsEnum) result |= 1 << 21;
            if (IsNullable) result |= 1 << 22;
            if (IsIgnored) result |= 1 << 23;
            if (IsFinal) result |= 1 << 24;
            if (HasSurrogate) result |= 1 << 25;
            if (HasConverter) result |= 1 << 26;
            if (IsISerializable) result |= 1 << 27;
            if (IsReference) result |= 1 << 28;
            if (IsDefaultSave) result |= 1 << 29;
            return result;
        }
        void IntToFlags(int flags)
        {
            Kind = (PrimitiveType)(flags & 0xFF);
            CollectionType = (ReflectCollectionType)((flags >> 8) & 0xFF);
            IsPointer = (flags & (1 << 16)) != 0;
            IsArray = (flags & (1 << 17)) != 0;
            IsGeneric = (flags & (1 << 18)) != 0;
            IsGenericTypeDefinition = (flags & (1 << 19)) != 0;
            IsGenericParameter = (flags & (1 << 20)) != 0;
            IsEnum = (flags & (1 << 21)) != 0;
            IsNullable = (flags & (1 << 22)) != 0;
            IsIgnored = (flags & (1 << 23)) != 0;
            IsFinal = (flags & (1 << 24)) != 0;
            HasSurrogate = (flags & (1 << 25)) != 0;
            HasConverter = (flags & (1 << 26)) != 0;
            IsISerializable = (flags & (1 << 27)) != 0;
            IsReference = (flags & (1 << 28)) != 0;
            IsDefaultSave = (flags & (1 << 29)) != 0;
        }

        #endregion

        #region Read() Write()

        internal void Read(ObjectReader reader)
        {
            IntToFlags(reader.Reader.ReadInt32());
            Type = KnownTypes.GetType(Kind);
            if (IsArray)
            {
                ArrayRank = (int)reader.Reader.ReadVInt();
                Element = (ReflectType)reader.Read(RReflectType, null);
                if (Element.Type != null)
                {
                    if (ArrayRank == 1) Type = Element.Type.MakeArrayType();
                    else Type = Element.Type.MakeArrayType(ArrayRank);
                }
            }
            else if (IsPointer)
            {
                Element = (ReflectType)reader.Read(RReflectType, null);
                if (Element.Type != null)
                    Type = Element.Type.MakePointerType();
            }
            else if (IsGenericParameter)
            {
                GenericParameterIndex = (int)reader.Reader.ReadVInt();
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    TypeName = (string)reader.Read(RString, null);
                    AssemblyName = (string)reader.Read(RString, null);
                    Type = KnownTypes.GetType(TypeName, AssemblyName);
                    if (reader.SkipMetaData)
                    {
                        if (Type == null)
                            throw new IOException("No MetaData with skip = true");
                        Initialize(Type);
                        return;
                    }
                }
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                    {
                        Element = (ReflectType)reader.Read(RReflectType, null);
                        int NArgs = (int)reader.Reader.ReadVInt();
                        var gArgs = new ReflectType[NArgs];
                        GenericArguments = gArgs;
                        for (int i = 0; i < NArgs; i++)
                            gArgs[i] = (ReflectType)reader.Read(RReflectType, null);
                    }
                }
                if (IsEnum)
                {
                    Element = (ReflectType)reader.Read(RReflectType, null);
                }
                else if (HasSurrogate)
                {
                    Surrogate = (ReflectType)reader.Read(RReflectType, null);
                }
                else if (!IsGeneric || IsGenericTypeDefinition)
                {
                    var fields = Type == null ? new Dictionary<string, FieldInfo>(0) : Type.GetRuntimeFields().ToDictionary(x => x.Name);
                    var properties = Type == null ? new Dictionary<string, PropertyInfo>(0) : Type.GetRuntimeProperties().ToDictionary(x => x.Name);
                    int NMembers = (int)reader.Reader.ReadVInt();
                    for (int i = 0; i < NMembers; i++)
                    {
                        var m = new Member();
                        m.Name = (string)reader.Read(RString, null);
                        m.Type = (ReflectType)reader.Read(RReflectType, null);
                        FieldInfo fi;
                        PropertyInfo pi;
                        if (fields.TryGetValue(m.Name, out fi) && fi.FieldType == m.Type.Type)
                        {
                            m.fInfo = fi;
                        }
                        else if (properties.TryGetValue(m.Name, out pi) && pi.PropertyType == m.Type.Type)
                        {
                            m.pInfo = pi;
                        }
                        Members.Add(m);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            Collection1 = (ReflectType)reader.Read(RReflectType, null);
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            Collection1 = (ReflectType)reader.Read(RReflectType, null);
                            Collection2 = (ReflectType)reader.Read(RReflectType, null);
                            break;
                    }
                }
            }
            InitHashCode();
            if (IsGeneric && !IsGenericTypeDefinition)
            {
                try
                {
                    if (Element.Type != null && GenericArguments.All(x => x.Type != null))
                        Type = Element.Type.MakeGenericType(GenericArguments.Select(x => x.Type).ToArray());
                }
                catch (Exception ex) { Logging.TraceKeys.Serialization.Error("Couldn't create concrete type", ex.Message); }

                if (IsDefaultSave)
                {
                    var fields = Type == null ? new Dictionary<string, FieldInfo>(0) : Type.GetRuntimeFields().ToDictionary(x => x.Name);
                    var properties = Type == null ? new Dictionary<string, PropertyInfo>(0) : Type.GetRuntimeProperties().ToDictionary(x => x.Name);
                    foreach (var em in Element.Members)
                    {
                        var m = new Member
                        {
                            Name = em.Name,
                            Type = em.Type,
                        };
                        if (em.Type.IsGenericParameter)
                        {
                            m.Type = GenericArguments[em.Type.GenericParameterIndex];
                        }
                        FieldInfo fi;
                        PropertyInfo pi;
                        if (fields.TryGetValue(em.Name, out fi) && fi.FieldType == m.Type.Type)
                        {
                            m.fInfo = fi;
                        }
                        else if (properties.TryGetValue(em.Name, out pi) && pi.PropertyType == m.Type.Type)
                        {
                            m.pInfo = pi;
                        }
                        Members.Add(m);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            Collection1 = Element.Collection1;
                            if (Collection1.IsGenericParameter)
                                Collection1 = GenericArguments[Element.Collection1.GenericParameterIndex];
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            Collection1 = Element.Collection1;
                            if (Collection1.IsGenericParameter)
                                Collection1 = GenericArguments[Element.Collection1.GenericParameterIndex];
                            Collection2 = Element.Collection2;
                            if (Collection2.IsGenericParameter)
                                Collection2 = GenericArguments[Element.Collection2.GenericParameterIndex];
                            break;
                    }
                }
            }
        }
        internal void Write(ObjectWriter writer)
        {
            writer.Writer.Write(FlagsToInt());
            if (IsArray)
            {
                writer.Writer.WriteVInt(ArrayRank);
                writer.Write(RReflectType, Element);
            }
            else if (IsPointer)
            {
                writer.Write(RReflectType, Element);
            }
            else if (IsGenericParameter)
            {
                writer.Writer.WriteVInt(GenericParameterIndex);
            }
            else if (Kind == PrimitiveType.Object)
            {
                if (!IsGeneric || IsGenericTypeDefinition)
                {
                    writer.Write(RString, TypeName);
                    writer.Write(RString, AssemblyName);
                    if (writer.SkipMetaData)
                        return;
                }
                if (IsGeneric)
                {
                    if (!IsGenericTypeDefinition)
                    {
                        writer.Write(RReflectType, Element);
                        writer.Writer.WriteVInt(GenericArguments.Count);
                        for (int i = 0; i < GenericArguments.Count; i++)
                            writer.Write(RReflectType, GenericArguments[i]);
                    }
                }
                if (IsEnum)
                {
                    writer.Write(RReflectType, Element);
                }
                else if (HasSurrogate)
                {
                    writer.Write(RReflectType, Surrogate);
                }
                else if (IsDefaultSave && (!IsGeneric || IsGenericTypeDefinition))
                {
                    writer.Writer.WriteVInt(Members.Count);
                    for (int i = 0; i < Members.Count; i++)
                    {
                        var m = Members[i];
                        writer.Write(RString, m.Name);
                        writer.Write(RReflectType, m.Type);
                    }
                    switch (CollectionType)
                    {
                        case ReflectCollectionType.ICollectionT:
                            writer.Write(RReflectType, Collection1);
                            break;
                        case ReflectCollectionType.IDictionaryKV:
                            writer.Write(RReflectType, Collection1);
                            writer.Write(RReflectType, Collection2);
                            break;
                    }
                }
            }
        }

        #endregion

        #region GetTypeConverter()

        public TypeConverter GetTypeConverter()
        {
#if __PCL__
                    throw new PlatformNotSupportedException("PCL");
#else
            if (converter != null)
                return converter;
            if (Type == null)
                return null;
            var attr = Type.GetTypeInfo().GetCustomAttribute<TypeConverterAttribute>();
            if (attr == null)
                return null;
            var tc = TypeDescriptor.GetConverter(Type);
            if (tc != null
                && tc.CanConvertFrom(typeof(string))
                && tc.CanConvertTo(typeof(string))
                )
            {
                converter = tc;
                return converter;
            }
            return null;
#endif
        }
        TypeConverter converter;

        #endregion

        #region surrogate helpers: TryGetSurrogate() TryGetOriginal()

        public bool TryGetSurrogate(object o, out object result)
        {
            result = null;
            if (o == null || Surrogate == null || KnownTypes.GetType(o) != Type)
                return false;

            result = Surrogate.Type.TryConstruct();
            var ti = typeof(ISurrogate<>).MakeGenericType(KnownTypes.GetType(o));
            var mi = ti.TryGetMethods(nameof(ISurrogate<int>.Initialize), null, KnownTypes.GetType(o)).FirstOrDefault();
            if (mi == null)
                throw new ArgumentException($"Couldn't Initialize surrogate<{Type.FullName}> for {o}");
            mi.Invoke(result, new[] { o });
            return true;
        }

        /// <summary>
        /// If 
        /// </summary>
        public bool TryGetOriginal(object o, out object result)
        {
            result = null;
            if (Kind != PrimitiveType.Object || Type == null || Surrogate == null || Surrogate.Type == null)
                return false;
            if (o == null)
                return false;

            if (KnownTypes.GetType(o) == Type)
            {
                result = o;
                return true;
            }
            if (KnownTypes.GetType(o) != Surrogate.Type)
                return false;

            var surr = (from t in Surrogate.Type.GetTypeHierarchy()
                        let ti = t.GetTypeInfo()
                        where ti.IsInterface
                        where !ti.IsGenericTypeDefinition
                        where ti.GetGenericTypeDefinition() == typeof(ISurrogate<>)
                        let ts = ti.GenericTypeArguments[0]
                        where Type == ts
                        select t
                        ).FirstOrDefault();
            if (surr != null)
            {
                var mi = surr.GetRuntimeMethod(nameof(ISurrogate<int>.Instantiate), Empty<Type>.Array);
                result = mi.Invoke(o, Empty<object>.Array);
                return true;
            }

            return false;
        }

        #endregion
    }
}
