﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Each <see cref="FastType"/> instance is associate with a particular .NET <see cref="System.Type"/>.
    /// It provides access to optimized members and constructor method, using System.Emit whenever possible for top performance.
    /// </summary>
    public sealed class FastType
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
            var type = PrimitiveConverter.GetType(kind);
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
            if (IsGenericMeta || IsAbstract)
                return null;

            if (fastCtor != null)
                return fastCtor();
            if (emtpy_constructor != null)
                return emtpy_constructor.Invoke(empty_params);

            if (!IsReference)
                return Activator.CreateInstance(Type);

#if NETFRAMEWORK
            return FormatterServices.GetSafeUninitializedObject(Type);
#else
            return RuntimeHelpers.GetUninitializedObject(Type);
#endif
        }

        FastMethod emtpy_constructor;
        object[] empty_params;
        Func<object> fastCtor;
        void SetConstructor()
        {
            var (ctor, count) = (
                from ci in Type.TryGetConstructors()
                let ciargs = ci.GetParameters()
                let cidefault = ciargs.Where(x => x.HasDefaultValue).Count()
                orderby ciargs.Length - cidefault ascending, ciargs.Length descending
                select (ci, ciargs.Length - cidefault)
                ).FirstOrDefault();

            switch ((CI: ctor, N: count))
            {
                case { CI: null } when Type.IsValueType:
                    fastCtor = EmitHelper.CreateParameterlessConstructorHandler(Type);
                    break;
                case { CI: not null, N: 0 }:
                    emtpy_constructor = new FastMethod(ctor);
                    empty_params = ctor.GetParameters().Select(x => x.DefaultValue).ToArray();
                    break;
            }
        }

#endregion

        /// <summary>
        /// Whether this is a real class (<c>false</c>), or a generic one missing arguments (<c>true</c>).
        /// </summary>
        public bool IsGenericMeta { get; private set; }

        /// <summary>
        /// Whether this is an abstract class or not.
        /// </summary>
        public bool IsAbstract { get; private set; }

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
            var ti = type.GetTypeInfo();
            if (!ti.IsGenericType)
                return false;
            return ti.GetGenericArguments().Any(x => x.GetTypeInfo().IsGenericParameter);
        }

        #endregion

        #region Initialize()

        void Initialize(Type type)
        {
            Type = type;
            var ti = type.GetTypeInfo();

            Kind = PrimitiveConverter.GetPrimitiveType(type);
            IsReference = !ti.IsValueType;
            BaseType = GetType(Type.GetTypeInfo().BaseType);
            IsMscorlib = IsFromMscorlib(type);
            IsAbstract = type.GetTypeInfo().IsAbstract || type.IsInterface || ((type.Attributes & TypeAttributes.Abstract) != 0);
            IsGenericMeta = IsUndefined(type);

            if (!type.IsArray && !ti.IsEnum && !IsAbstract && !IsGenericMeta)
                SetConstructor();
        }

        #endregion

        #region GetRuntimeMembers()

        /// <summary>
        /// Enumerate all <see cref="FastMember"/> of this class and all of its base classes.
        /// </summary>
        public IEnumerable<FastMember> GetRuntimeMembers()
        {
            var previous = new HashSet<FastMember>();

            var p = this;
            while (p != null)
            {
                foreach (var m in p.DeclaredMembers)
                {
                    if (m.IsOverride)
                        previous.Add(m.BaseMember);

                    if (previous.Contains(m))
                        continue;

                    yield return m;
                }
                p = p.BaseType;
            }
        }

        #endregion

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
                                var m = new FastMember(this, pi, null);
                                result.Add(m);
                            }
                            foreach (var pi in ti.DeclaredProperties)
                            {
                                if (pi.GetMethod == null || pi.GetMethod.GetParameters().Length != 0)
                                    continue;

                                FastMember baseMember = null;
                                if (!pi.IsBaseProperty())
                                {
                                    var pb = pi.GetBaseBroperty();
                                    if (pb != pi)
                                        baseMember = GetType(pb.DeclaringType).DeclaredMembers[pi.Name];
                                }
                                var mt = FastType.GetType(pi.PropertyType);
                                var m = new FastMember(this, pi, baseMember);
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
    public sealed class FastMember : IMember
    {
        internal FastMember(FastType declaring, MemberInfo member, FastMember baseMember)
        {
            DeclaringType = declaring;
            Name = member.Name;
            Member = member;
            BaseMember = baseMember ?? this;
            if (member is FieldInfo)
            {
                var pi = (FieldInfo)member;
                IsField = true;
                Type = FastType.GetType(pi.FieldType);
                IsPublic = pi.IsPublic;
                IsPublicSetter = pi.IsPublic;
                CanSet = !pi.IsLiteral;
                IsStatic = pi.IsStatic;
            }
            else
            {
                var pi = (PropertyInfo)member;
                Type = FastType.GetType(pi.PropertyType);
                IsPublic = pi.GetMethod.IsPublic;
                IsPublicSetter = pi.SetMethod?.IsPublic ?? false;
                IsField = false;
                CanSet = pi.SetMethod != null;
                IsStatic = pi.GetMethod.IsStatic;
            }
            if (!DeclaringType.IsGenericMeta)
            {
                InitializeAccessor();
                InitializeStructAccessor();
            }
        }

        /// <summary>
        /// This is the member name for the member, i.e. <see cref="MemberInfo.Name"/>.
        /// </summary>
        public string Name { get; }

        public FastType DeclaringType { get; }

        /// <summary>
        /// If this member is an override, that will be the original declaration
        /// </summary>
        public FastMember BaseMember { get; }
        public bool IsBase => BaseMember == this;
        public bool IsOverride => BaseMember != this;

        public T GetAttribute<T>()
            where T : Attribute
            => GetAttributes<T>().FirstOrDefault();

        public IEnumerable<T> GetAttributes<T>()
            where T : Attribute
            => GetAttributes().Where(x => x is T).Cast<T>();

        public IEnumerable<Attribute> GetAttributes() => PathToBase().SelectMany(x => Attribute.GetCustomAttributes(x.Member));

        public IEnumerable<FastMember> PathToBase()
        {
            var type = Type;
            var me = this;
            while (true)
            {
                if (me != null)
                    yield return me;

                if (BaseMember == null || me == BaseMember)
                    yield break;

                type = type.BaseType;
                me = type.DeclaredMembers[Name];
            }
        }

        /// <summary>
        /// Whether or not this describe a static member.
        /// </summary>
        public bool IsStatic { get;  }

        /// <summary>
        /// This is the info for the declared type of this member, i.e. either of
        /// <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.
        /// </summary>
        public FastType Type { get; }

        /// <summary>
        /// Whether this is a public member or not
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        /// Whether this is a public member or not
        /// </summary>
        public bool IsPublicSetter { get; }

        /// <summary>
        /// Whether this is a field or a property
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        /// Whether this member can be set. Will be <c>false</c> if the member is a property without setter, 
        /// or if the field is a literal (set at compile time), <c>true</c> otherwise.
        /// </summary>
        public bool CanSet { get; }

        /// <summary>
        /// Return the reflection member associated with this instance, be it a <see cref="FieldInfo"/> or <see cref="PropertyInfo"/>.
        /// </summary>
        public MemberInfo Member { get; }

        // performance fields
        Action<object, object> setter;
        Func<object, object> getter;
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
        Func<object, Guid> getterGuid;
        Func<object, bool> getterBool;
        Func<object, char> getterChar;
        Func<object, byte> getterByte;
        Func<object, sbyte> getterSByte;
        Func<object, short> getterInt16;
        Func<object, ushort> getterUInt16;
        Func<object, int> getterInt32;
        Func<object, uint> getterUInt32;
        Func<object, long> getterInt64;
        Func<object, ulong> getterUInt64;
        Func<object, float> getterSingle;
        Func<object, double> getterDouble;
        Func<object, decimal> getterDecimal;

        PropertyInfo pInfo;
        FieldInfo fInfo;

        #region InitializeStructAccessor() InitializeAccessor()

        void InitializeStructAccessor()
        {
            switch (Type.Kind)
            {
                default:
                case PrimitiveType.None:
                case PrimitiveType.Object:
                case PrimitiveType.String:
                case PrimitiveType.Bytes:
                    break;
                case PrimitiveType.Guid:
                    InitFastGetter<Guid>(Member, ref getterGuid, ref setterGuid);
                    break;
                case PrimitiveType.Bool:
                    InitFastGetter<bool>(Member, ref getterBool, ref setterBool);
                    break;
                case PrimitiveType.Char:
                    InitFastGetter<char>(Member, ref getterChar, ref setterChar);
                    break;
                case PrimitiveType.Byte:
                    InitFastGetter<byte>(Member, ref getterByte, ref setterByte);
                    break;
                case PrimitiveType.SByte:
                    InitFastGetter<sbyte>(Member, ref getterSByte, ref setterSByte);
                    break;
                case PrimitiveType.Int16:
                    InitFastGetter<short>(Member, ref getterInt16, ref setterInt16);
                    break;
                case PrimitiveType.UInt16:
                    InitFastGetter<ushort>(Member, ref getterUInt16, ref setterUInt16);
                    break;
                case PrimitiveType.Int32:
                    InitFastGetter<int>(Member, ref getterInt32, ref setterInt32);
                    break;
                case PrimitiveType.UInt32:
                    InitFastGetter<uint>(Member, ref getterUInt32, ref setterUInt32);
                    break;
                case PrimitiveType.Int64:
                    InitFastGetter<long>(Member, ref getterInt64, ref setterInt64);
                    break;
                case PrimitiveType.UInt64:
                    InitFastGetter<ulong>(Member, ref getterUInt64, ref setterUInt64);
                    break;
                case PrimitiveType.Single:
                    InitFastGetter<float>(Member, ref getterSingle, ref setterSingle);
                    break;
                case PrimitiveType.Double:
                    InitFastGetter<double>(Member, ref getterDouble, ref setterDouble);
                    break;
                case PrimitiveType.Decimal:
                    InitFastGetter<decimal>(Member, ref getterDecimal, ref setterDecimal);
                    break;
            }
        }
        static void InitFastGetter<T>(MemberInfo mi, ref Func<object, T> getter, ref Action<object, T> setter)
        {
            if (mi is PropertyInfo)
            {
                InitFastGetter<T>((PropertyInfo)mi, ref getter, ref setter);
            }
            else if (mi is FieldInfo)
            {
                InitFastGetter<T>((FieldInfo)mi, ref getter, ref setter);
            }
        }
        static void InitFastGetter<T>(PropertyInfo pi, ref Func<object, T> getter, ref Action<object, T> setter)
        {
            getter = EmitHelper.CreatePropertyGetter<T>(pi);
            if (pi.SetMethod != null)
                setter = EmitHelper.CreatePropertySetter<T>(pi);
        }
        static void InitFastGetter<T>(FieldInfo pi, ref Func<object, T> getter, ref Action<object, T> setter)
        {
            if (pi.IsLiteral)
            {
                var value = (T)pi.GetValue(null);
                getter = (x) => value;
            }
            else
            {
                getter = EmitHelper.CreateFieldGetter<T>(pi);
                setter = EmitHelper.CreateFieldSetter<T>(pi);
            }
        }

        void InitializeAccessor()
        {
            if (Member is PropertyInfo)
            {
                pInfo = (PropertyInfo)Member;
                getter = EmitHelper.CreatePropertyGetterHandler(pInfo);
                if (pInfo.SetMethod != null)
                {
                    setter = EmitHelper.CreatePropertySetterHandler(pInfo);
                }
            }
            else
            {
                fInfo = (FieldInfo)Member;
                if (fInfo.IsLiteral)
                {
                    var value = fInfo.GetValue(null);
                    getter = (x) => value;
                }
                else
                {
                    getter = EmitHelper.CreateFieldGetterHandler(fInfo);
                    setter = EmitHelper.CreateFieldSetterHandler(fInfo);
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
            if (getter != null)
                return getter(instance);
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

            if (setter != null)
            {
                setter(instance, value);
                return true;
            }
            return false;
        }

        #endregion

        #region typed known structs: Get/Set Guid/Bool/Char/...()

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool FastSet<T>(object instance, T value, Action<object, T> setter)
        {
            if (setter != null)
            {
                setter(instance, value);
                return true;
            }
            else { return false; }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T FastGet<T>(object instance, Func<object, T> getter)
        {
            if (getter != null) { return getter(instance); }
            else { return default(T); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetGuid(object instance, Guid value)
        {
            return FastSet<Guid>(instance, value, setterGuid);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid GetGuid(object instance)
        {
            return FastGet<Guid>(instance, getterGuid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetBool(object instance, bool value)
        {
            return FastSet<bool>(instance, value, setterBool);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBool(object instance)
        {
            return FastGet<bool>(instance, getterBool);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetChar(object instance, char value)
        {
            return FastSet<char>(instance, value, setterChar);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char GetChar(object instance)
        {
            return FastGet<char>(instance, getterChar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetInt8(object instance, byte value)
        {
            return FastSet<byte>(instance, value, setterByte);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetInt8(object instance)
        {
            return FastGet<byte>(instance, getterByte);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetUInt8(object instance, sbyte value)
        {
            return FastSet<sbyte>(instance, value, setterSByte);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte GetUInt8(object instance)
        {
            return FastGet<sbyte>(instance, getterSByte);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetInt16(object instance, short value)
        {
            return FastSet<short>(instance, value, setterInt16);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short GetInt16(object instance)
        {
            return FastGet<short>(instance, getterInt16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetUInt16(object instance, ushort value)
        {
            return FastSet<ushort>(instance, value, setterUInt16);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetUInt16(object instance)
        {
            return FastGet<ushort>(instance, getterUInt16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetInt32(object instance, int value)
        {
            return FastSet<int>(instance, value, setterInt32);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt32(object instance)
        {
            return FastGet<int>(instance, getterInt32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetUInt32(object instance, uint value)
        {
            return FastSet<uint>(instance, value, setterUInt32);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetUInt32(object instance)
        {
            return FastGet<uint>(instance, getterUInt32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetInt64(object instance, long value)
        {
            return FastSet<long>(instance, value, setterInt64);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetInt64(object instance)
        {
            return FastGet<long>(instance, getterInt64);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetUInt64(object instance, ulong value)
        {
            return FastSet<ulong>(instance, value, setterUInt64);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetUInt64(object instance)
        {
            return FastGet<ulong>(instance, getterUInt64);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetSingle(object instance, float value)
        {
            return FastSet<float>(instance, value, setterSingle);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetSingle(object instance)
        {
            return FastGet<float>(instance, getterSingle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetDouble(object instance, double value)
        {
            return FastSet<double>(instance, value, setterDouble);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetDouble(object instance)
        {
            return FastGet<double>(instance, getterDouble);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetDecimal(object instance, decimal value)
        {
            return FastSet<decimal>(instance, value, setterDecimal);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetDecimal(object instance)
        {
            return FastGet<decimal>(instance, getterDecimal);
        }

        #endregion
    }

    #endregion
}