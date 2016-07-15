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
        /// Gets the <see cref="FastType"/> associated with any given type.
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

        public static implicit operator FastType(Type type) { return GetType(type); }
        public static implicit operator Type(FastType type) { return type?.Type; }

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

        ConstructorInfo emtpy_constructor;
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
#if __NET__ || __NETCORE__
            if (ps.Length == 0)
            {
                fastCtor = EmitHelper.CreateParameterlessConstructorHandler(ctor);
                return;
            }
#endif
            var cargs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (!p.HasDefaultValue)
                    return;
                cargs[i] = p.DefaultValue;
            }
            emtpy_constructor = ctor;
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
        /// Member list of this class.
        /// </summary>
        public MemberList<FastMember> Members { get; } = new MemberList<FastMember>();

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

        #region Initialize()

        void Initialize(Type type)
        {
            Type = type;
            Kind = KnownTypes.GetKind(type);
            IsReference = Type.GetTypeInfo().IsByRef;
            BaseType = GetType(Type.GetTypeInfo().BaseType);
            IsMscorlib = IsFromMscorlib(type);
            IsAbstract = type.GetTypeInfo().IsAbstract;

            var ti = type.GetTypeInfo();
            IsGenericMeta = false;
            if (ti.IsGenericType)
            {
                if (ti.IsGenericParameter)
                    IsGenericMeta = true;
#if __PCL__
                throw new PlatformNotSupportedException("PCL");
#else
                else
                    IsGenericMeta = ti.GetGenericArguments().Any(x => x.GetTypeInfo().IsGenericParameter);
#endif
            }

            if (type.IsPointer)
                IsIgnored = true;
            else if (typeof(Delegate).IsBaseClass(type) || type == typeof(IntPtr) || type == typeof(Enum))
                IsIgnored = true;
            if (IsIgnored)
                return;

            if (type.IsArray || ti.IsEnum)
                return;

            SetConstructor();

            // REMARK: skip static members as they proved problematic...
            foreach (var pi in ti.DeclaredFields)
            {
                if (pi.IsStatic)
                    continue;
                var mt = FastType.GetType(pi.FieldType);
                if (mt.IsIgnored)
                    continue;
                var m = new FastMember
                {
                    Name = pi.Name,
                    Type = mt,
                    IsPublic = pi.IsPublic,
                    IsField = true,
                    CanSet = true,
                    Member = pi,
                };
                if (!IsGenericMeta)
                    m.SetMember(pi);
                Members.Add(m);
            }
            foreach (var pi in ti.DeclaredProperties)
            {
                if (pi.GetMethod == null || pi.GetMethod.GetParameters().Length != 0)
                    continue;
                if (pi.GetMethod.IsStatic)
                    continue;
                var mt = FastType.GetType(pi.PropertyType);
                if (mt.IsIgnored)
                    continue;
                var m = new FastMember
                {
                    Name = pi.Name,
                    Type = mt,
                    IsPublic = pi.GetMethod.IsPublic,
                    IsField = false,
                    CanSet = pi.SetMethod != null,
                    Member = pi,
                };
                if (!IsGenericMeta)
                    m.SetMember(pi);
                Members.Add(m);
            }
        }

        #endregion
    }

    #region class FastMember

    /// <summary>
    /// Represent a member of this type, i.e. a property or field that will be serialized.
    /// Also this will use fast member accessor generated with Emit on platform supporting it.
    /// </summary>
    public partial class FastMember : IMember
    {
        internal FastMember() { }

        /// <summary>
        /// This is the member name for the member, i.e. <see cref="MemberInfo.Name"/>.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// This is the info for the declared type of this member, i.e. either of
        /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
        /// </summary>
        public FastType Type { get; internal set; }

        /// <summary>
        /// Whether this is a public member or not
        /// </summary>
        public bool IsPublic { get; internal set; }

        /// <summary>
        /// Whether this is a field or a property
        /// </summary>
        public bool IsField { get; internal set; }

        /// <summary>
        /// Whether this member can be set. Will be <c>false</c> if the member is a property without setter. 
        /// <c>true</c> otherwise.
        /// </summary>
        public bool CanSet { get; internal set; }

        /// <summary>
        /// Return the reflection member associated with this instance, be it a <see cref="FieldInfo"/> or <see cref="PropertyInfo"/>.
        /// </summary>
        public MemberInfo Member { get; internal set; }

        PropertyInfo pInfo;
        FieldInfo fInfo;

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
#endif

        #region SetMember()

        internal void SetMember(MemberInfo mi)
        {
            if (mi is PropertyInfo)
            {
                pInfo = (PropertyInfo)mi;
#if __NET__ || __NETCORE__
                getter = EmitHelper.CreatePropertyGetterHandler(pInfo);
                if (pInfo.SetMethod != null)
                {
                    setter = EmitHelper.CreatePropertySetterHandler(pInfo);
                    switch (Type.Kind)
                    {
                        case PrimitiveType.Guid:
                            hasFastSetter = true;
                            setterGuid = EmitHelper.CreatePropertySetter<Guid>(pInfo);
                            break;
                        case PrimitiveType.Bool:
                            hasFastSetter = true;
                            setterBool = EmitHelper.CreatePropertySetter<bool>(pInfo);
                            break;
                        case PrimitiveType.Char:
                            hasFastSetter = true;
                            setterChar = EmitHelper.CreatePropertySetter<char>(pInfo);
                            break;
                        case PrimitiveType.Byte:
                            hasFastSetter = true;
                            setterByte = EmitHelper.CreatePropertySetter<byte>(pInfo);
                            break;
                        case PrimitiveType.SByte:
                            hasFastSetter = true;
                            setterSByte = EmitHelper.CreatePropertySetter<sbyte>(pInfo);
                            break;
                        case PrimitiveType.Int16:
                            hasFastSetter = true;
                            setterInt16 = EmitHelper.CreatePropertySetter<short>(pInfo);
                            break;
                        case PrimitiveType.UInt16:
                            hasFastSetter = true;
                            setterUInt16 = EmitHelper.CreatePropertySetter<ushort>(pInfo);
                            break;
                        case PrimitiveType.Int32:
                            hasFastSetter = true;
                            setterInt32 = EmitHelper.CreatePropertySetter<int>(pInfo);
                            break;
                        case PrimitiveType.UInt32:
                            hasFastSetter = true;
                            setterUInt32 = EmitHelper.CreatePropertySetter<uint>(pInfo);
                            break;
                        case PrimitiveType.Int64:
                            hasFastSetter = true;
                            setterInt64 = EmitHelper.CreatePropertySetter<long>(pInfo);
                            break;
                        case PrimitiveType.UInt64:
                            hasFastSetter = true;
                            setterUInt64 = EmitHelper.CreatePropertySetter<ulong>(pInfo);
                            break;
                        case PrimitiveType.Single:
                            hasFastSetter = true;
                            setterSingle = EmitHelper.CreatePropertySetter<float>(pInfo);
                            break;
                        case PrimitiveType.Double:
                            hasFastSetter = true;
                            setterDouble = EmitHelper.CreatePropertySetter<double>(pInfo);
                            break;
                        case PrimitiveType.Decimal:
                            hasFastSetter = true;
                            setterDecimal = EmitHelper.CreatePropertySetter<decimal>(pInfo);
                            break;
                    }
                }
#endif
            }
            else
            {
                fInfo = (FieldInfo)mi;
#if __NET__ || __NETCORE__
                getter = EmitHelper.CreateFieldGetterHandler(fInfo);
                setter = EmitHelper.CreateFieldSetterHandler(fInfo);
                switch (Type.Kind)
                {
                    case PrimitiveType.Guid:
                        hasFastSetter = true;
                        setterGuid = EmitHelper.CreateFieldSetter<Guid>(fInfo);
                        break;
                    case PrimitiveType.Bool:
                        hasFastSetter = true;
                        setterBool = EmitHelper.CreateFieldSetter<bool>(fInfo);
                        break;
                    case PrimitiveType.Char:
                        hasFastSetter = true;
                        setterChar = EmitHelper.CreateFieldSetter<char>(fInfo);
                        break;
                    case PrimitiveType.Byte:
                        hasFastSetter = true;
                        setterByte = EmitHelper.CreateFieldSetter<byte>(fInfo);
                        break;
                    case PrimitiveType.SByte:
                        hasFastSetter = true;
                        setterSByte = EmitHelper.CreateFieldSetter<sbyte>(fInfo);
                        break;
                    case PrimitiveType.Int16:
                        hasFastSetter = true;
                        setterInt16 = EmitHelper.CreateFieldSetter<short>(fInfo);
                        break;
                    case PrimitiveType.UInt16:
                        hasFastSetter = true;
                        setterUInt16 = EmitHelper.CreateFieldSetter<ushort>(fInfo);
                        break;
                    case PrimitiveType.Int32:
                        hasFastSetter = true;
                        setterInt32 = EmitHelper.CreateFieldSetter<int>(fInfo);
                        break;
                    case PrimitiveType.UInt32:
                        hasFastSetter = true;
                        setterUInt32 = EmitHelper.CreateFieldSetter<uint>(fInfo);
                        break;
                    case PrimitiveType.Int64:
                        hasFastSetter = true;
                        setterInt64 = EmitHelper.CreateFieldSetter<long>(fInfo);
                        break;
                    case PrimitiveType.UInt64:
                        hasFastSetter = true;
                        setterUInt64 = EmitHelper.CreateFieldSetter<ulong>(fInfo);
                        break;
                    case PrimitiveType.Single:
                        hasFastSetter = true;
                        setterSingle = EmitHelper.CreateFieldSetter<float>(fInfo);
                        break;
                    case PrimitiveType.Double:
                        hasFastSetter = true;
                        setterDouble = EmitHelper.CreateFieldSetter<double>(fInfo);
                        break;
                    case PrimitiveType.Decimal:
                        hasFastSetter = true;
                        setterDecimal = EmitHelper.CreateFieldSetter<decimal>(fInfo);
                        break;
                }
#endif
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
            if (instance == null)
                return null;
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
            if (instance == null  || !Type.Type.IsInstanceOf(value))
                return false;
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