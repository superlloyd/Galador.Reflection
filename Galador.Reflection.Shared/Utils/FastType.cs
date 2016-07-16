using Galador.Reflection.Serialization;
using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Each <see cref="FastType"/> instance is associate with a particular .NET <see cref="System.Type"/>.
    /// It provides access to optimized members and constructor method, using System.Emit whenever possible for top performance.
    /// </summary>
    [NotSerialized]
    public class FastType
    {
        FastType() { }

        #region GetType()

        /// <summary>
        /// Gets the <see cref="FastType"/> associated with <typeparamref name="T"/> type.
        /// </summary>
        public static FastType GetType<T>() { return GetType(typeof(T)); }

        /// <summary>
        /// Gets the <see cref="FastType"/> associated with <paramref name="type"/> type.
        /// </summary>
        public static FastType GetType(Type type)
        {
            if (type == null)
                return null;
            lock (sReflectCache)
            {
                FastType result;
                if (!sReflectCache.TryGetValue(type, out result))
                {
                    result = new FastType();
                    sReflectCache[type] = result;
                    result.Initialize(type);
                }
                return result;
            }
        }
        static Dictionary<Type, FastType> sReflectCache = new Dictionary<System.Type, FastType>();

        /// <summary>
        /// Get the <see cref="FastType"/> associated with each primitive type. Except for object, where it returns null.
        /// </summary>
        public static FastType GetType(PrimitiveType kind)
        {
            var type = KnownTypes.GetType(kind);
            return GetType(type);
        }

        #endregion

        #region TryConstruct() SetConstructor()

        /// <summary>
        /// Will create and return a new instance of <see cref="Type"/> associated to this <see cref="FastType"/>
        /// By using either the default constructor (i.e. constructor with no parameter or where all parameters have 
        /// a default value) or creating a so called "uninitialized object". It might return null if it fails.
        /// </summary>
        public object TryConstruct()
        {
            if (IsGenericMeta || IsAbstract || IsIgnored)
                return null;

#if __NET__ || __NETCORE__
            if (fastCtor != null)
                return fastCtor();
#endif
            if (emtpy_constructor != null)
                return emtpy_constructor.Invoke(empty_params);

            if (!IsReference)
                return Activator.CreateInstance(Type);

#if __PCL__
            throw new PlatformNotSupportedException("PCL"); 
#elif __NETCORE__
            return null;
#else
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(Type);
#endif
        }

        FastMethod emtpy_constructor;
        object[] empty_params;
#if __NET__ || __NETCORE__
        Func<object> fastCtor;
#endif
        void SetConstructor()
        {
            var ctor = Type.TryGetConstructors().OrderBy(x => x.GetParameters().Length).FirstOrDefault();
            if (ctor == null)
            {
#if __NET__ || __NETCORE__
                if (Type.GetTypeInfo().IsValueType)
                    fastCtor = EmitHelper.CreateParameterlessConstructorHandler(Type);
#endif
                return;
            }

            var ps = ctor.GetParameters();
            var cargs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (!p.HasDefaultValue)
                    return;
                cargs[i] = p.DefaultValue;
            }
            emtpy_constructor = new FastMethod(ctor);
            empty_params = cargs;
        }

        #endregion

        /// <summary>
        /// Whether or not this is associated with a <see cref="Type"/> which is a pointer, delegate or IntPtr.
        /// </summary>
        internal bool IsIgnored { get; private set; }

        /// <summary>
        /// Whether this is a real class (<c>false</c>), or a generic one missing arguments (<c>true</c>).
        /// </summary>
        public bool IsGenericMeta { get; private set; }

        /// <summary>
        /// Whether this is an abstract class or not.
        /// </summary>
        public bool IsAbstract { get; set; }

        /// <summary>
        /// Whether or not this is a type passed by reference.
        /// </summary>
        public bool IsReference { get; private set; }

        /// <summary>
        /// The possible well know type, as an enum.
        /// </summary>
        public PrimitiveType Kind { get; private set; }

        /// <summary>
        /// The type associated with this instance.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// Gets the <see cref="FastType"/> associated with the BaseType.
        /// </summary>
        public FastType BaseType { get; private set; }

        /// <summary>
        /// Whether or not this type is defined in mscorlib. Assembly name can be omitted for such type when serializing.
        /// Also they don't need be generated when creating serialized type code.
        /// </summary>
        public bool IsMscorlib { get; private set; }

        /// <summary>
        /// Check whether a type is from mscorlib base library, or not. Impacting the information needed for serialization.
        /// </summary>
        public static bool IsFromMscorlib(Type type) { return type.GetTypeInfo().Assembly == MSCORLIB; }
        internal static readonly Assembly MSCORLIB = typeof(object).GetTypeInfo().Assembly;

        #region IsUndefined()

        /// <summary>
        /// Whether <paramref name="type"/> is a generic type with generic argument, such as <c>List&lt;&gt;</c>.
        /// </summary>
        public static bool IsUndefined(Type type)
        {
            if (type.IsGenericParameter)
                return true;
#if __PCL__
                throw new PlatformNotSupportedException("PCL");
#else
            var ti = type.GetTypeInfo();
            if (!ti.IsGenericType)
                return false;
            return ti.GetGenericArguments().Any(x => x.GetTypeInfo().IsGenericParameter);
#endif
        }

        #endregion

        #region Initialize()

        void Initialize(Type type)
        {
            Type = type;
            var ti = type.GetTypeInfo();

            Kind = KnownTypes.GetKind(type);
            IsReference = !ti.IsValueType;
            BaseType = GetType(Type.GetTypeInfo().BaseType);
            IsMscorlib = IsFromMscorlib(type);
            IsAbstract = type.GetTypeInfo().IsAbstract;
            IsGenericMeta = IsUndefined(type);

            if (type.IsPointer)
                IsIgnored = true;
            else if (typeof(Delegate).IsBaseClass(type) || type == typeof(IntPtr) || type == typeof(Enum))
                IsIgnored = true;
            if (IsIgnored)
                return;

            if (!type.IsArray && !ti.IsEnum)
                SetConstructor();
        }

        #endregion

        /// <summary>
        /// Enumerate all <see cref="FastMember"/> of this class and all of its base classes.
        /// </summary>
        public IEnumerable<FastMember> GetRuntimeMembers()
        {
            var p = this;
            while (p != null)
            {
                foreach (var m in p.DeclaredMembers)
                    yield return m;
                p = p.BaseType;
            }
        }

        #region DeclaredMembers

        /// <summary>
        /// Members list for this <see cref="Type"/> as <see cref="FastMember"/>.
        /// </summary>
        public MemberList<FastMember> DeclaredMembers
        {
            get
            {
                if (members == null)
                    lock (this)
                        if (members == null)
                        {
                            var result = new MemberList<FastMember>();
                            var ti = Type.GetTypeInfo();
                            foreach (var pi in ti.DeclaredFields)
                            {
                                var mt = FastType.GetType(pi.FieldType);
                                if (mt.IsIgnored)
                                    continue;
                                var m = new FastMember(pi);
                                result.Add(m);
                            }
                            foreach (var pi in ti.DeclaredProperties)
                            {
                                if (pi.GetMethod == null || pi.GetMethod.GetParameters().Length != 0)
                                    continue;
                                var mt = FastType.GetType(pi.PropertyType);
                                if (mt.IsIgnored)
                                    continue;
                                var m = new FastMember(pi);
                                result.Add(m);
                            }
                            members = result;
                        }
                return members;
            }
        }
        MemberList<FastMember> members;

        #endregion

        #region DeclaredMethods

        /// <summary>
        /// Gets all the declared methods of <see cref="Type"/> as <see cref="FastMethod"/>.
        /// </summary>
        public IReadOnlyList<FastMethod> DeclaredMethods
        {
            get
            {
                if (methods == null)
                    lock (this)
                        if (methods == null)
                            methods = Type.GetTypeInfo().DeclaredMethods.Select(x => new FastMethod(x)).ToArray();
                return methods;
            }
        }
        FastMethod[] methods;

        #endregion

        #region DeclaredConstructors

        /// <summary>
        /// Gets all the declared constructors of <see cref="Type"/> as <see cref="FastMethod"/>.
        /// </summary>
        public IReadOnlyList<FastMethod> DeclaredConstructors
        {
            get
            {
                if (ctors == null)
                    lock (this)
                        if (ctors == null)
                            ctors = Type.GetTypeInfo().DeclaredConstructors.Select(x => new FastMethod(x)).ToArray();
                return ctors;
            }
        }
        FastMethod[] ctors;

        #endregion
    }

    #region class FastMember

    /// <summary>
    /// Represent a member of this type, i.e. a property or field that will be serialized.
    /// Also this will use fast member accessor generated with Emit on platform supporting it.
    /// </summary>
    public partial class FastMember : IMember
    {
        internal FastMember(MemberInfo member)
        {
            Name = member.Name;
            Member = member;
            if (member is FieldInfo)
            {
                var pi = (FieldInfo)member;
                IsField = true;
                Type = FastType.GetType(pi.FieldType);
                IsPublic = pi.IsPublic;
                CanSet = !pi.IsLiteral;
                IsStatic = pi.IsStatic;
            }
            else
            {
                var pi = (PropertyInfo)member;
                Type = FastType.GetType(pi.PropertyType);
                IsPublic = pi.GetMethod.IsPublic;
                IsField = false;
                CanSet = pi.SetMethod != null;
                IsStatic = pi.GetMethod.IsStatic;
            }
            SetMember(member);
        }

        /// <summary>
        /// This is the member name for the member, i.e. <see cref="MemberInfo.Name"/>.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Whether or not this describe a static member.
        /// </summary>
        public bool IsStatic { get; private set; }

        /// <summary>
        /// This is the info for the declared type of this member, i.e. either of
        /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
        /// </summary>
        public FastType Type { get; private set; }

        /// <summary>
        /// Whether this is a public member or not
        /// </summary>
        public bool IsPublic { get; private set; }

        /// <summary>
        /// Whether this is a field or a property
        /// </summary>
        public bool IsField { get; private set; }

        /// <summary>
        /// Whether this member can be set. Will be <c>false</c> if the member is a property without setter, 
        /// or if the field is a literal (set at compile time), <c>true</c> otherwise.
        /// </summary>
        public bool CanSet { get; private set; }

        /// <summary>
        /// Return the reflection member associated with this instance, be it a <see cref="FieldInfo"/> or <see cref="PropertyInfo"/>.
        /// </summary>
        public MemberInfo Member { get; private set; }

        // performance fields, depends on platform
#if __NET__ || __NETCORE__
        Action<object, object> setter;
        Func<object, object> getter;
        bool hasFastSetter;
        Action<object, Guid> setterGuid;
        Action<object, bool> setterBool;
        Action<object, char> setterChar;
        Action<object, byte> setterByte;
        Action<object, sbyte> setterSByte;
        Action<object, short> setterInt16;
        Action<object, ushort> setterUInt16;
        Action<object, int> setterInt32;
        Action<object, uint> setterUInt32;
        Action<object, long> setterInt64;
        Action<object, ulong> setterUInt64;
        Action<object, float> setterSingle;
        Action<object, double> setterDouble;
        Action<object, decimal> setterDecimal;
#else
        PropertyInfo pInfo;
        FieldInfo fInfo;
#endif

        #region SetMember()

        internal void SetMember(MemberInfo mi)
        {
            if (mi is PropertyInfo)
            {
                var pi = (PropertyInfo)mi;
#if __NET__ || __NETCORE__
                getter = EmitHelper.CreatePropertyGetterHandler(pi);
                if (pi.SetMethod != null)
                {
                    setter = EmitHelper.CreatePropertySetterHandler(pi);
                    switch (Type.Kind)
                    {
                        case PrimitiveType.Guid:
                            hasFastSetter = true;
                            setterGuid = EmitHelper.CreatePropertySetter<Guid>(pi);
                            break;
                        case PrimitiveType.Bool:
                            hasFastSetter = true;
                            setterBool = EmitHelper.CreatePropertySetter<bool>(pi);
                            break;
                        case PrimitiveType.Char:
                            hasFastSetter = true;
                            setterChar = EmitHelper.CreatePropertySetter<char>(pi);
                            break;
                        case PrimitiveType.Byte:
                            hasFastSetter = true;
                            setterByte = EmitHelper.CreatePropertySetter<byte>(pi);
                            break;
                        case PrimitiveType.SByte:
                            hasFastSetter = true;
                            setterSByte = EmitHelper.CreatePropertySetter<sbyte>(pi);
                            break;
                        case PrimitiveType.Int16:
                            hasFastSetter = true;
                            setterInt16 = EmitHelper.CreatePropertySetter<short>(pi);
                            break;
                        case PrimitiveType.UInt16:
                            hasFastSetter = true;
                            setterUInt16 = EmitHelper.CreatePropertySetter<ushort>(pi);
                            break;
                        case PrimitiveType.Int32:
                            hasFastSetter = true;
                            setterInt32 = EmitHelper.CreatePropertySetter<int>(pi);
                            break;
                        case PrimitiveType.UInt32:
                            hasFastSetter = true;
                            setterUInt32 = EmitHelper.CreatePropertySetter<uint>(pi);
                            break;
                        case PrimitiveType.Int64:
                            hasFastSetter = true;
                            setterInt64 = EmitHelper.CreatePropertySetter<long>(pi);
                            break;
                        case PrimitiveType.UInt64:
                            hasFastSetter = true;
                            setterUInt64 = EmitHelper.CreatePropertySetter<ulong>(pi);
                            break;
                        case PrimitiveType.Single:
                            hasFastSetter = true;
                            setterSingle = EmitHelper.CreatePropertySetter<float>(pi);
                            break;
                        case PrimitiveType.Double:
                            hasFastSetter = true;
                            setterDouble = EmitHelper.CreatePropertySetter<double>(pi);
                            break;
                        case PrimitiveType.Decimal:
                            hasFastSetter = true;
                            setterDecimal = EmitHelper.CreatePropertySetter<decimal>(pi);
                            break;
                    }
                }
#else
                pInfo = pi;
#endif
            }
            else
            {
                var fi = (FieldInfo)mi;
                if (fi.IsLiteral)
                {
#if __NET__ || __NETCORE__
                    var value = fi.GetValue(null);
                    getter = (x) => value;
#else
                    fInfo = fi;
#endif
                }
                else
                {
#if __NET__ || __NETCORE__
                    getter = EmitHelper.CreateFieldGetterHandler(fi);
                    setter = EmitHelper.CreateFieldSetterHandler(fi);
                    switch (Type.Kind)
                    {
                        case PrimitiveType.Guid:
                            hasFastSetter = true;
                            setterGuid = EmitHelper.CreateFieldSetter<Guid>(fi);
                            break;
                        case PrimitiveType.Bool:
                            hasFastSetter = true;
                            setterBool = EmitHelper.CreateFieldSetter<bool>(fi);
                            break;
                        case PrimitiveType.Char:
                            hasFastSetter = true;
                            setterChar = EmitHelper.CreateFieldSetter<char>(fi);
                            break;
                        case PrimitiveType.Byte:
                            hasFastSetter = true;
                            setterByte = EmitHelper.CreateFieldSetter<byte>(fi);
                            break;
                        case PrimitiveType.SByte:
                            hasFastSetter = true;
                            setterSByte = EmitHelper.CreateFieldSetter<sbyte>(fi);
                            break;
                        case PrimitiveType.Int16:
                            hasFastSetter = true;
                            setterInt16 = EmitHelper.CreateFieldSetter<short>(fi);
                            break;
                        case PrimitiveType.UInt16:
                            hasFastSetter = true;
                            setterUInt16 = EmitHelper.CreateFieldSetter<ushort>(fi);
                            break;
                        case PrimitiveType.Int32:
                            hasFastSetter = true;
                            setterInt32 = EmitHelper.CreateFieldSetter<int>(fi);
                            break;
                        case PrimitiveType.UInt32:
                            hasFastSetter = true;
                            setterUInt32 = EmitHelper.CreateFieldSetter<uint>(fi);
                            break;
                        case PrimitiveType.Int64:
                            hasFastSetter = true;
                            setterInt64 = EmitHelper.CreateFieldSetter<long>(fi);
                            break;
                        case PrimitiveType.UInt64:
                            hasFastSetter = true;
                            setterUInt64 = EmitHelper.CreateFieldSetter<ulong>(fi);
                            break;
                        case PrimitiveType.Single:
                            hasFastSetter = true;
                            setterSingle = EmitHelper.CreateFieldSetter<float>(fi);
                            break;
                        case PrimitiveType.Double:
                            hasFastSetter = true;
                            setterDouble = EmitHelper.CreateFieldSetter<double>(fi);
                            break;
                        case PrimitiveType.Decimal:
                            hasFastSetter = true;
                            setterDecimal = EmitHelper.CreateFieldSetter<decimal>(fi);
                            break;
                    }
#else
                    fInfo = fi;
#endif
                }
            }
        }

        #endregion

        #region public: GetValue() SetValue()

        /// <summary>
        /// Gets the value of this member for the given instance.
        /// </summary>
        /// <param name="instance">The instance from which to take the value.</param>
        /// <returns>The value of the member.</returns>
        public object GetValue(object instance)
        {
            if (IsStatic)
            {
                instance = null;
            }
            else
            {
                if (instance == null)
                    return null;
            }
#if __NET__ || __NETCORE__
            if (getter != null)
                return getter(instance);
#else
            if (pInfo != null && pInfo.GetMethod != null)
                return pInfo.GetValue(instance);
            if (fInfo != null)
                return fInfo.GetValue(instance);
#endif
            return null;
        }

        /// <summary>
        /// Sets the value of this member (if possible) for the given instance.
        /// </summary>
        /// <param name="instance">The instance on which the member value will be set.</param>
        /// <param name="value">The value that must be set.</param>
        /// <returns>Whether the value has been set, or not.</returns>
        public bool SetValue(object instance, object value)
        {
            if (!CanSet)
                return false;
            if (IsStatic)
            {
                instance = null;
            }
            else
            {
                if (instance == null || !Type.Type.IsInstanceOf(value))
                    return false;
            }

#if __NET__ || __NETCORE__
            if (setter != null)
            {
                setter(instance, value);
                return true;
            }
#else
            if (pInfo != null && pInfo.SetMethod != null)
            {
                pInfo.SetValue(instance, value);
                return true;
            }
            else if (fInfo != null)
            {
                fInfo.SetValue(instance, value);
                return true;
            }
#endif
            return false;
        }

        #endregion

        #region TryFastReadSet()

        /// <summary>
        /// Am even faster way to et known value type, using strongly typed accessor on platform supporting it.
        /// </summary>
        /// <param name="reader">The source of the value</param>
        /// <param name="instance">The instance which member would be set</param>
        /// <returns>Whether the value could be set. If not the <paramref name="reader"/> won't be read.</returns>
        public bool TryFastReadSet(ObjectReader reader, object instance)
        {
#if __NET__ || __NETCORE__
            if (hasFastSetter && instance != null)
            {
                switch (Type.Kind)
                {
                    case PrimitiveType.Guid:
                        setterGuid(instance, reader.Reader.ReadGuid());
                        break;
                    case PrimitiveType.Bool:
                        setterBool(instance, reader.Reader.ReadBool());
                        break;
                    case PrimitiveType.Char:
                        setterChar(instance, reader.Reader.ReadChar());
                        break;
                    case PrimitiveType.Byte:
                        setterByte(instance, reader.Reader.ReadByte());
                        break;
                    case PrimitiveType.SByte:
                        setterSByte(instance, reader.Reader.ReadSByte());
                        break;
                    case PrimitiveType.Int16:
                        setterInt16(instance, reader.Reader.ReadInt16());
                        break;
                    case PrimitiveType.UInt16:
                        setterUInt16(instance, reader.Reader.ReadUInt16());
                        break;
                    case PrimitiveType.Int32:
                        setterInt32(instance, reader.Reader.ReadInt32());
                        break;
                    case PrimitiveType.UInt32:
                        setterUInt32(instance, reader.Reader.ReadUInt32());
                        break;
                    case PrimitiveType.Int64:
                        setterInt64(instance, reader.Reader.ReadInt64());
                        break;
                    case PrimitiveType.UInt64:
                        setterUInt64(instance, reader.Reader.ReadUInt64());
                        break;
                    case PrimitiveType.Single:
                        setterSingle(instance, reader.Reader.ReadSingle());
                        break;
                    case PrimitiveType.Double:
                        setterDouble(instance, reader.Reader.ReadDouble());
                        break;
                    case PrimitiveType.Decimal:
                        setterDecimal(instance, reader.Reader.ReadDecimal());
                        break;
                }
                return true;
            }
#endif
            return false;
        }

        #endregion
    }

    #endregion
}