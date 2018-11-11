using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public enum RuntimeCollectionType : byte
    {
        None,
        IList,
        IDictionary,
        ICollectionT,
        IDictionaryKV,
    }

    /// <summary>
    /// A class the runtime information useful and available to serialization
    /// </summary>
    public sealed class RuntimeType
    {
        readonly static object locker = new object();

        #region private: static .ctor() Register()

        static RuntimeType()
        {
            Register(KnownAssemblies.Current);
            KnownAssemblies.AssemblyLoaded += a => Register(a);
        }

        static void Register(IEnumerable<Assembly> assemblies)
        {
            lock (locker)
            {
                foreach (var ass in assemblies)
                    Register(ass);
            }
        }
        static void Register(Assembly assembly)
        {
            lock (locker)
            {
                foreach (var ti in assembly.DefinedTypes)
                    Register(ti.AsType());
            }
        }
        static void Register(Type type)
        {
            lock (locker)
            {
                RegisterNameAlias(type);
                RegisterSurrogates(type);
            }
        }

        static void RegisterNameAlias(Type type)
        {
            var nattr = type.GetTypeInfo().GetCustomAttribute<SerializationNameAttribute>();
            if (nattr != null)
            {
                if (typeAliases.TryGetValue(nattr, out var prev))
                {
                    Log.Error($"Ambiguous SerializationName found {nattr} match: {prev}, {type}");
                }
                else
                {
                    typeAliases[nattr] = type;
                }
            }
        }
        static readonly Dictionary<SerializationNameAttribute, Type> typeAliases = new Dictionary<SerializationNameAttribute, Type>();


        static void RegisterSurrogates(Type type)
        {
            foreach (var t in GetSurrogatedTypes(type))
            {
                if (typeToSurrogate.TryGetValue(t, out var prev))
                {
                    Log.Warning($"Duplicate Surrogates Found for {t}: {type}, {prev}");
                }
                else
                {
                    typeToSurrogate[t] = type;
                }
            }
        }
        static readonly Dictionary<Type, Type> typeToSurrogate = new Dictionary<Type, Type>();

        static IEnumerable<Type> GetSurrogatedTypes(Type type)
        {
            if (type.GetTypeInfo().IsAbstract)
                yield break;
            if (!type.TryGetConstructors().Any())
                yield break;
            foreach (var t in type.GetTypeHierarchy())
            {
                if (!t.GetTypeInfo().IsGenericType)
                    continue;
                var t2 = t.GetGenericTypeDefinition();
                if (t2 != typeof(ISurrogate<>))
                    continue;
                var t3 = t.GetTypeInfo().GenericTypeArguments[0];
                if (t3.GetTypeInfo().IsGenericType)
                {
                    if (!type.GetTypeInfo().IsGenericType)
                        continue;
                    if (t3.GenericTypeArguments.Length != type.GetTypeInfo().GenericTypeParameters.Length)
                        continue;
                    if (Enumerable.Range(0, type.GetTypeInfo().GenericTypeParameters.Length)
                        .Any(i => t3.GenericTypeArguments[i] != type.GetTypeInfo().GenericTypeParameters[i]))
                        continue;
                    var t4 = t3.GetGenericTypeDefinition();
                    t3 = t4;
                }
                yield return t3;
            }
        }

        #endregion

        #region public: class Surrogate, private: GetSurrogate()

        public class SurrogateInfo
        {
            internal SurrogateInfo(Type target, Type surrogate)
            {
                SurrogateType = RuntimeType.GetType(surrogate);

                var tInterface = typeof(ISurrogate<>).MakeGenericType(target);
                Initialize = tInterface.TryGetMethods(nameof(ISurrogate<int>.Initialize), null, target).First();
                Instantiate = tInterface.GetRuntimeMethod(nameof(ISurrogate<int>.Instantiate), Array.Empty<Type>());
            }

            public RuntimeType SurrogateType { get; }
            readonly MethodInfo Initialize, Instantiate;

            public object Convert(object source)
            {
                var result = SurrogateType.FastType.TryConstruct();
                Initialize.Invoke(result, new[] { source });
                return result;
            }

            public object Revert(object surrogate) => Instantiate.Invoke(surrogate, Array.Empty<object>());
        }

        static SurrogateInfo GetSurrogate(RuntimeType type) => GetSurrogate(type?.Type);

        static SurrogateInfo GetSurrogate(Type type)
        {
            if (type == null)
                return null;
            lock (locker)
            {
                if (typeToSurrogate.TryGetValue(type, out var surrogate))
                    return new SurrogateInfo(type, surrogate);

                if (type.GetTypeInfo().IsGenericType)
                {
                    var t2 = type.GetGenericTypeDefinition();
                    if (typeToSurrogate.TryGetValue(type.GetGenericTypeDefinition(), out var gSurrogate))
                    {
                        surrogate = gSurrogate.MakeGenericType(type.GenericTypeArguments);
                        return new SurrogateInfo(type, surrogate);
                    }
                }

                return null;
            }
        }

        #endregion

        #region public: GetType()

        public static RuntimeType GetType(string name, string assembly)
        {
            lock (locker)
            {
                var sn = new SerializationNameAttribute(name, assembly);
                if (typeAliases.TryGetValue(sn, out var type))
                    return GetType(type);

                type = KnownAssemblies.GetType(name, assembly);
                if (type != null)
                    return GetType(type);

                return null;
            }
        }

        public static RuntimeType GetType(object value)
        {
            if (value == null)
                return GetType(typeof(object));
            if (value is Type
                || value is TypeData
                || value is RuntimeType)
                return GetType(typeof(Type));
            return GetType(value.GetType());
        }

        public static RuntimeType GetType(PrimitiveType kind)
        {
            switch (kind)
            {
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    return null;
                default:
                    return GetType(PrimitiveConverter.GetType(kind));
            }
        }

        public static RuntimeType GetType<T>() => GetType(typeof(T));

        public static RuntimeType GetType(Type type)
        {
            if (type == null)
                return null;
            lock (locker)
            {
                if (!sReflectCache.TryGetValue(type, out var result))
                {
                    result = new RuntimeType();
                    sReflectCache[type] = result;
                    result.Initialize(type);
                }
                return result;
            }
        }
        static Dictionary<Type, RuntimeType> sReflectCache = new Dictionary<System.Type, RuntimeType>();

        #endregion

        #region private: ctor() IsIgnored() Initialize()

        private RuntimeType() { }

        private static bool IsIgnored(Type type)
        {
            if (type.BaseType == typeof(Enum))
                return false;

            if (type.BaseType != null && IsIgnored(type.BaseType))
                return true;

            if (type.IsPointer)
                return true;

            if (type == typeof(Delegate) 
                || type == typeof(IntPtr) 
                || type == typeof(Enum)
                || type == typeof(RuntimeType)
                || type == typeof(TypeData)
                )
                return true;

            if (type.GetCustomAttribute<NotSerializedAttribute>() != null)
                return true;

            if (type.IsArray && IsIgnored(type.GetElementType()))
                return true;

            if (type.IsGenericType 
                && !type.IsGenericTypeDefinition 
                && type.GetGenericArguments().Any(x => IsIgnored(x)))
                return true;

            return false;
        }

        private void Initialize(Type type)
        {
            Type = type;
            IsSupported = !IsIgnored(type);
            if (!IsSupported)
                return;

            Kind = PrimitiveConverter.GetPrimitiveType(type);
            FastType = FastType.GetType(type);

            switch (Kind)
            {
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    break;
                case PrimitiveType.Type:
                case PrimitiveType.String:
                case PrimitiveType.Bytes:
                    IsReference = true;
                    IsSealed = true;
                    return;
                default:
                    return;
            }

            void CaptureName()
            {
                var nattr = type.GetTypeInfo().GetCustomAttribute<SerializationNameAttribute>();
                if (nattr != null)
                {
                    FullName = nattr.FullName;
                    Assembly = nattr.AssemblyName;
                }
                else
                {
                    FullName = type.FullName;
                    if (!FastType.IsMscorlib)
                        Assembly = type.Assembly.GetName().Name;
                }
            }

            if (type.IsArray)
            {
                IsReference = true;
                IsSealed = true;
                ArrayRank = type.GetArrayRank();
                Element = GetType(type.GetElementType());
            }
            else if (type.IsEnum)
            {
                IsEnum = true;
                Element = GetType(type.GetEnumUnderlyingType());
                CaptureName();
            }
            else if (type.IsGenericParameter)
            {
                IsGenericParameter = true;
                var pargs = type.DeclaringType.GetTypeInfo().GetGenericArguments();
                for (int i = 0; i < pargs.Length; i++)
                    if (pargs[i] == type)
                    {
                        GenericParameterIndex = i;
                        break;
                    }
            }
            else
            {
                IsSealed = type.IsSealed;
                IsReference = !type.IsValueType;
                Surrogate = GetSurrogate(type);
                BaseType = GetType(type.BaseType);
                IsInterface = type.IsInterface;
                IsAbstract = type.IsAbstract;
                IsISerializable = typeof(ISerializable).IsBaseClass(type);
                Converter = GetTypeConverter();

                if (type.IsGenericType)
                {
                    IsGeneric = true;
                    if (type.IsGenericTypeDefinition)
                    {
                        IsGenericTypeDefinition = true;
                        CaptureName();
                    }
                    else
                    {
                        var def = type.GetGenericTypeDefinition();
                        Element = GetType(def);
                        IsNullable = def == typeof(Nullable<>);
                    }
                    GenericParameters = type.GetGenericArguments()
                        .Select(x => GetType(x))
                        .ToList()
                        .AsReadOnly();
                }
                else
                {
                    CaptureName();
                }

                if (Converter == null && Surrogate == null && !IsInterface && !IsISerializable)
                {
                    var settings = type.GetCustomAttribute<SerializationSettingsAttribute>() ?? SerializationSettingsAttribute.Defaults;
                    foreach (var m in FastType.DeclaredMembers)
                    {
                        if (m.IsStatic || m.IsOverride)
                            continue;
                        var rType = GetType(m.Type.Type);
                        if (!rType.IsSupported)
                            continue;
                        if (m.GetAttribute<NonSerializedAttribute>() != null)
                            continue;
                        if (m.GetAttribute<SerializableAttribute>() == null)
                        {
                            if (m.IsField && m.IsPublic && !settings.IncludePublicFields)
                                continue;
                            if (m.IsField && !m.IsPublic && !settings.IncludePrivateFields)
                                continue;
                            if (!m.IsField && m.IsPublic && !settings.IncludePublicProperties)
                                continue;
                            if (!m.IsField && !m.IsPublic && !settings.IncludePrivateProperties)
                                continue;
                        }
                        if (!m.CanSet && !m.Type.IsReference)
                            continue;
                        var name = m.GetAttribute<SerializationMemberNameAttribute>()?.MemberName;
                        Members.Add(new Member(this)
                        {
                            Name = name ?? m.Name,
                            Type = rType,
                            RuntimeMember = m,
                        });

                        var interfaces = type.GetInterfaces();
                        var iDictKV = interfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)).FirstOrDefault();
                        var iColT = interfaces.Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)).FirstOrDefault();
                        if (settings.IncludeDictionaryInterface && iDictKV != null)
                        {
                            CollectionType = RuntimeCollectionType.IDictionaryKV;
                            Collection1 = GetType(iDictKV.GenericTypeArguments[0]);
                            Collection2 = GetType(iDictKV.GenericTypeArguments[1]);
                        }
                        else if (settings.IncludeListInterface && iColT != null)
                        {
                            CollectionType = RuntimeCollectionType.ICollectionT;
                            Collection1 = GetType(iColT.GenericTypeArguments[0]);
                        }
                        else if (settings.IncludeDictionaryInterface && interfaces.Contains(typeof(IDictionary)))
                        {
                            CollectionType = RuntimeCollectionType.IDictionary;
                        }
                        else if (settings.IncludeListInterface && interfaces.Contains(typeof(IList)))
                        {
                            CollectionType = RuntimeCollectionType.IList;
                        }
                    }
                }
            }
        }

        #endregion

        public PrimitiveType Kind { get; private set; }
        public Type Type { get; private set; }
        public FastType FastType { get; private set; }

        public string FullName { get; private set; }
        public string Assembly { get; private set; }

        public RuntimeType Element { get; private set; }
        public RuntimeType BaseType { get; private set; }

        public SurrogateInfo Surrogate { get; private set; }
        public TypeConverter Converter { get; private set; }

        public bool IsSupported { get; private set; }
        public bool IsSealed { get; private set; }
        public bool IsReference { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsEnum { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsGeneric { get; private set; }
        public bool IsGenericParameter { get; private set; }
        public bool IsGenericTypeDefinition { get; private set; }
        public bool IsInterface { get; private set; }
        public bool IsAbstract { get; private set; }
        public bool IsISerializable { get; private set; }

        public RuntimeCollectionType CollectionType { get; private set; }
        public RuntimeType Collection1 { get; private set; }
        public RuntimeType Collection2 { get; private set; }
        internal FastMethod writeColT, writeDictKV;

        public int ArrayRank { get; private set; }
        public int GenericParameterIndex { get; private set; }
        public IReadOnlyList<RuntimeType> GenericParameters { get; private set; }
        public MemberList<Member> Members { get; } = new MemberList<Member>();

        #region public: TypeData()

        public TypeData TypeData()
        {
            if (mTypeData == null)
                lock (locker)
                    if (mTypeData == null)
                    {
                        mTypeData = new TypeData();
                        mTypeData.Initialize(this);
                    }
            return mTypeData;
        }
        TypeData mTypeData;

        #endregion

        #region RuntimeMembers

        public IEnumerable<Member> RuntimeMembers
        {
            get
            {
                if (BaseType != null)
                    foreach (var m in BaseType.Members)
                        yield return m;
                foreach (var m in Members)
                    yield return m;
            }
        }

        #endregion

        #region private: GetTypeConverter()

        TypeConverter GetTypeConverter()
        {
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
        }
        TypeConverter converter;

        #endregion

        #region public: class Member

        /// <summary>
        /// Represent a member of this type, i.e. a property or field that will be serialized.
        /// </summary>
        public class Member : IMember
        {
            internal Member(RuntimeType owner) { DeclaringType = owner; }

            public RuntimeType DeclaringType { get; }

            /// <summary>
            /// This is the member name for the member, i.e. <see cref="MemberInfo.Name"/>.
            /// </summary>
            public string Name { get; internal set; }

            /// <summary>
            /// This is the info for the declared type of this member, i.e. either of
            /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
            /// </summary>
            public RuntimeType Type { get; internal set; }

            /// <summary>
            /// This is the info for the declared type of this member, i.e. either of
            /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
            /// </summary>
            /// <remarks>This property can be null when deserializing and matching member can be found.</remarks>
            public FastMember RuntimeMember { get; internal set; }
        }

        #endregion
    }
}
