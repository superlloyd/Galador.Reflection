using Galador.Reflection.Serialization.IO;
using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SRS = System.Runtime.Serialization;


namespace Galador.Reflection.Serialization
{
    public class Reader : Context, IDisposable
    {
        readonly IPrimitiveReader input;
        readonly internal SerializationSettings settings = new SerializationSettings();
        readonly ulong VERSION;

        public Reader(IPrimitiveReader input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.VERSION = input.ReadVUInt();
            switch (VERSION)
            {
                case 0x01_02:
                    break;
                default:
                    throw new ArgumentException($"Unsupported version number {VERSION:X4}");
            }
        }

        internal static readonly ReadArgs AObject = new ReadArgs(RObject);
        internal static readonly ReadArgs AString = new ReadArgs(RString);
        internal static readonly ReadArgs AType = new ReadArgs(RType);

        public void Dispose()
        {
            input.Dispose();
        }

        public object Read()
        {
            readRaw = false;
            return Read(AObject);
        }

        public object ReadRaw()
        {
            readRaw = true;
            return Read(AObject);
        }

        public T Read<T>(T suggested = default(T))
        {
            readRaw = false;
            var args = new ReadArgs(RObject.TypeData(), RuntimeType.GetType(typeof(T)), suggested);
            return (T)Read(args);
        }

        object Read(ReadArgs args)
        {
            if (readRecurseDepth++ == 0)
            {
                var sFlags = input.ReadVInt();
                settings.FromFlags((int)sFlags);
            }
            try
            {
                var result = ReadImpl(args);
                if (readRecurseDepth > 1)
                    return result;
                return AsType(result);
            }
            finally
            {
                if (--readRecurseDepth == 0)
                {
                    foreach (var item in Objects.OfType<IDeserialized>())
                    {
                        item.Deserialized(GetLost(item));
                    }
                    foreach (var item in Objects.OfType<SRS.IDeserializationCallback>())
                    {
                        // Known Issue: native .NET serialization doesn't support breaking generic type parameter change
                        try { item.OnDeserialization(this); }
                        catch (InvalidCastException ex) { Log.Error(ex); } 
                    }
                }
            }
        }
        // read settings
        int readRecurseDepth = 0;
        bool readRaw;

        internal class ReadArgs
        {
            public readonly TypeData TypeData;
            public readonly RuntimeType TypeHint;
            public readonly object Instance;

            #region ctor

            public ReadArgs(TypeData data, RuntimeType hint = null, object instance = null)
            {
                TypeData = data;
                TypeHint = hint;
                Instance = instance;
            }
            public ReadArgs(RuntimeType type)
            {
                TypeData = type.TypeData();
            }

            #endregion

            #region InstanceType(bool raw)

            public RuntimeType InstanceType(bool raw)
            {
                if (Instance != null)
                    return Serialization.RuntimeType.GetType(Instance);

                switch (TypeData.Kind)
                {
                    case PrimitiveType.None:
                    case PrimitiveType.Object:
                        if (raw)
                            return null;
                        break;
                    default:
                        return Serialization.RuntimeType.GetType(TypeData.Kind);
                }

                var p = TypeData;
                while (p != null)
                {
                    var target = p.RuntimeType();
                    if (target != null && !target.IsInterface && !target.IsAbstract)
                    {
                        if (TypeHint != null)
                        {
                            if (TypeHint.Type.IsBaseClass(target.Type))
                                return target;
                        }
                        else
                        {
                            return target;
                        }
                    }
                    p = p.BaseType;
                }

                if (TypeHint != null && !TypeHint.IsAbstract && !TypeHint.IsInterface)
                    return TypeHint;

                return null;
            }

            #endregion
        }

        internal object ReadImpl(ReadArgs args)
        {
            // check id first
            ulong oid = 0;
            if (args.TypeData.IsReference)
            {
                oid = input.ReadVUInt();
                if (TryGetObject(oid, out var result))
                    return result;
            }

            // if expected is not final
            if (args.TypeData.IsReference && !args.TypeData.IsSealed)
                args = new ReadArgs((TypeData)ReadImpl(AType), args.TypeHint, args.Instance);

            object ReturnRegister(object value)
            {
                if (oid != 0)
                    Register(oid, value);
                return value;
            }

            // only proceed further if type is supported
            if (!args.TypeData.IsSupported)
                return ReturnRegister(new ObjectData(args.TypeData));

            // dispatch to appropriate read method
            if (args.TypeData.Surrogate != null)
            {
                return ReturnRegister(ReadSurrogate(args));
            }
            else if (args.TypeData.HasConverter && !settings.IgnoreTypeConverter)
            {
                return ReturnRegister(ReadConverter(args));
            }
            else if (args.TypeData.IsISerializable && !settings.IgnoreISerializable)
            {
                return ReturnRegister(ReadISerializable(args));
            }
            else
            {
                switch (args.TypeData.Kind)
                {
                    default:
                    case PrimitiveType.None:
                        throw new InvalidOperationException("shouldn't be there");
                    case PrimitiveType.Object:
                        if (args.TypeData.IsArray)
                        {
                            return ReadArray(args, oid);
                        }
                        else if (args.TypeData.IsNullable)
                        {
                            object o = null;
                            var isNotNull = input.ReadBool();
                            if (isNotNull)
                                o = ReadImpl(new ReadArgs(args.TypeData.GenericParameters[0]));
                            return ReturnRegister(o);
                        }
                        else if (args.TypeData.IsEnum)
                        {
                            var val = ReadImpl(new ReadArgs(args.TypeData.Element));
                            var eType = args.InstanceType(readRaw);
                            if (eType != null)
                            {
                                val = Enum.ToObject(eType.Type, val);
                            }
                            else
                            {
                                // leave it as is?
                                // or return an ObjectData?
                            }
                            return ReturnRegister(val);
                        }
                        else
                        {
                            return ReturnRegister(ReadObject(args, oid));
                        }
                    case PrimitiveType.Type:
                        {
                            var result = new TypeData();
                            ReturnRegister(result);
                            result.Read(this, input);
                            return result;
                        }
                    case PrimitiveType.String:
                        return ReturnRegister(input.ReadString());
                    case PrimitiveType.Bytes:
                        return ReturnRegister(input.ReadBytes());
                    case PrimitiveType.Guid:
                        return ReturnRegister(input.ReadGuid());
                    case PrimitiveType.Bool:
                        return ReturnRegister(input.ReadBool());
                    case PrimitiveType.Char:
                        return ReturnRegister(input.ReadChar());
                    case PrimitiveType.Byte:
                        return ReturnRegister(input.ReadByte());
                    case PrimitiveType.SByte:
                        return ReturnRegister(input.ReadSByte());
                    case PrimitiveType.Int16:
                        return ReturnRegister(input.ReadInt16());
                    case PrimitiveType.UInt16:
                        return ReturnRegister(input.ReadUInt16());
                    case PrimitiveType.Int32:
                        return ReturnRegister(input.ReadInt32());
                    case PrimitiveType.UInt32:
                        return ReturnRegister(input.ReadUInt32());
                    case PrimitiveType.Int64:
                        return ReturnRegister(input.ReadInt64());
                    case PrimitiveType.UInt64:
                        return ReturnRegister(input.ReadUInt64());
                    case PrimitiveType.Single:
                        return ReturnRegister(input.ReadSingle());
                    case PrimitiveType.Double:
                        return ReturnRegister(input.ReadDouble());
                    case PrimitiveType.Decimal:
                        return ReturnRegister(input.ReadDecimal());
                }
            }
        }

        object ReadISerializable(ReadArgs args)
        {
            var info = new SRS.SerializationInfo(typeof(object), new SRS.FormatterConverter());
            var ctx = new SRS.StreamingContext(SRS.StreamingContextStates.Persistence);
            var N = (int)input.ReadVInt();
            for (int i = 0; i < N; i++)
            {
                var s = (string)ReadImpl(AString);
                var o = ReadImpl(AObject);
                info.AddValue(s, o);
            }

            if (args.Instance != null)
            {
                var ctor = args.Instance.GetType().TryGetConstructors(info.GetType(), ctx.GetType()).FirstOrDefault();
                // No FastMethod(): couldn't manage to call constructor on existing instance
                // Also, dare to do call constructor on existing instance!!
                if (ctor != null)
                {
                    ctor.Invoke(args.Instance, new object[] { info, ctx });
                    return args.Instance;
                }
                else
                {
                    Log.Warning($"ignored ISerializable data");
                    GetLost(args.Instance).SerializationInfo = info;
                    return args.Instance;
                }
            }

            var rtype = args.InstanceType(readRaw);
            if (rtype != null)
            {
                var ctor = rtype?.Type.TryGetConstructors(info.GetType(), ctx.GetType()).FirstOrDefault();
                if (ctor != null)
                {
                    return ctor.Invoke(new object[] { info, ctx });
                }

                // should we do that?
                var result = rtype?.FastType.TryConstruct();
                if (result != null)
                {
                    Log.Warning($"ignored ISerializable data");
                    GetLost(result).SerializationInfo = info;
                    return result;
                }
            }

            return new ObjectData(args.TypeData)
            { 
                Info = info,
            };
        }

        object ReadConverter(ReadArgs args)
        {
            var s = (string)ReadImpl(AString);

            var converter = args.InstanceType(readRaw)?.Converter;
            if (converter != null)
                return converter.ConvertFromInvariantString(s);

            return new ObjectData(args.TypeData)
            {
                ConverterString = s,
            };
        }

        object ReadSurrogate(ReadArgs args)
        {
            var o = ReadImpl(AObject);

            var surrogate = args.InstanceType(readRaw)?.Surrogate;
            if (surrogate != null)
            {
                return surrogate.Revert(o);
            }

            return new ObjectData(args.TypeData)
            {
                SurrogateObject = o,
            };
        }

        object ReadArray(ReadArgs args, ulong oid)
        {
            var ranks = Enumerable.Range(0, args.TypeData.ArrayRank)
                .Select(x => (int)input.ReadVInt())
                .ToArray();

            var eArgs = new ReadArgs(args.TypeData.Element);
            var eType = eArgs.InstanceType(readRaw);
            Array array;
            if (eType != null)
            {
                array = Array.CreateInstance(eType.Type, ranks);
            }
            else
            {
                array = Array.CreateInstance(typeof(ObjectData), ranks);
            }
            Register(oid, array);

            if (ranks.All(x => x > 0))
            {
                var indices = new int[ranks.Length];
                do
                {
                    var value = ReadImpl(eArgs);
                    array.SetValue(value, indices);
                }
                while (Inc());

                bool Inc()
                {
                    for (int i = ranks.Length - 1; i >= 0; i--)
                    {
                        indices[i]++;
                        if (indices[i] < ranks[i])
                            return true;
                        indices[i] = 0;
                    }
                    return false;
                }
            }

            return array;
        }

        object ReadObject(ReadArgs args, ulong oid)
        {
            var type = args.InstanceType(readRaw);
            var o = args.Instance ?? type?.FastType.TryConstruct();
            var od = o == null ? new ObjectData(args.TypeData) : null;
            if (oid > 0) Register(oid, o ?? od);

            var candidates = new List<RuntimeType.Member>();
            var matches = new List<TypeData.Member>();
            RuntimeType.Member FindRuntimeMember(TypeData.Member m)
            {
                if (type == null)
                    return null;

                candidates.Clear();
                var aType = type;
                while (aType != null)
                {
                    var aM = aType.Members[m.Name];
                    if (aM != null)
                    {
                        candidates.Add(aM);
                    }
                    aType = aType.BaseType;
                }
                if (candidates.Count == 0)
                    return null;
                if (candidates.Count == 1)
                    return candidates[0];

                matches.Clear();
                var aTypeData = m.DeclaringType;
                while (aTypeData != null)
                {
                    var aM = aTypeData.Members[m.Name];
                    if (aM != null)
                    {
                        matches.Add(aM);
                    }
                    aTypeData = aTypeData.BaseType;
                }

                var ic = candidates.Count - matches.Count + matches.IndexOf(m);
                if (ic < 0)
                    ic = 0;
                return candidates[ic];
            }

            foreach (var m in args.TypeData.RuntimeMembers)
            {
                var p = FindRuntimeMember(m);
                var pt = m.Type;
                var margs = new ReadArgs(pt, p?.Type, p?.RuntimeMember.GetValue(o));

                var value = ReadImpl(margs);
                if (o != null)
                {
                    if (p == null 
                        || !p.RuntimeMember.CanSet
                        || !p.RuntimeMember.SetValue(o, AsType(value)))
                    {
                        Log.Warning($"Can't restore Member {args.TypeData.FullName}.{m.Name}");
                        GetLost(o).Members.Add(new LostData.Member(m, AsType(value)));
                    }
                }
                else
                {
                    od.Members.Add(new ObjectData.Member
                    {
                        Name = m.Name,
                        Value = value,
                    });
                }
            }

            switch (args.TypeData.CollectionType)
            {
                case RuntimeCollectionType.IList:
                    ReadList(o ?? od);
                    break;
                case RuntimeCollectionType.IDictionary:
                    ReadDict(o ?? od);
                    break;
                case RuntimeCollectionType.ICollectionT:
                    ReadCollection(o ?? od, type, args.TypeData);
                    break;
                case RuntimeCollectionType.IDictionaryKV:
                    ReadDict(o ?? od, type, args.TypeData);
                    break;
            }

            return o ?? od;
        }
        void ReadList(object o)
        {
            var isRO = input.ReadBool();
            if (isRO)
                return;

            var list = o as IList;
            List<object> ilist = null;
            if (o is ObjectData od)
            {
                ilist = new List<object>();
                od.IList = ilist.AsReadOnly();
            }
            else if (list == null)
            {
                var lost = GetLost(o);
                ilist = new List<object>();
                lost.IList = ilist.AsReadOnly();
            }

            var count = (int)input.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var value = ReadImpl(AObject);
                if (list != null)
                {
                    list.Add(AsType(value));
                }
                else
                {
                    ilist.Add(value);
                }
            }
        }
        void ReadDict(object o)
        {
            var isRO = input.ReadBool();
            if (isRO)
                return;

            var list = o as IDictionary;
            List<(object, object)> ilist = null;
            if (o is ObjectData od)
            {
                ilist = new List<(object, object)>();
                od.IDictionary = ilist.AsReadOnly();
            }
            else if (list == null)
            {
                var lost = GetLost(o);
                ilist = new List<(object, object)>();
                lost.IDictionary = ilist.AsReadOnly();
            }

            var count = (int)input.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var key = ReadImpl(AObject);
                var value = ReadImpl(AObject);
                if (list != null)
                {
                    list.Add(AsType(key), AsType(value));
                }
                else if (ilist != null)
                {
                    ilist.Add((key, value));
                }
            }
        }
        void ReadCollection(object o, RuntimeType oType, TypeData tSrc)
        {
            var isRO = input.ReadBool();
            if (isRO)
                return;

            List<object> ilist = null;
            if (o is ObjectData od)
            {
                ilist = new List<object>();
                od.IList = ilist.AsReadOnly();
            }
            else
            {
                var rtValue = tSrc.Collection1.RuntimeType();
                bool overrides = false;
                if (oType.CollectionType == RuntimeCollectionType.ICollectionT)
                {
                    overrides = true;
                    rtValue = oType.Collection1;
                }
                if (rtValue != null)
                {
                    var m = GetType().TryGetMethods(nameof(ReadCollectionT), new[] { rtValue.Type }, typeof(object), typeof(TypeData), typeof(RuntimeType)).First();
                    m.Invoke(this, new object[] { o, tSrc, overrides ? oType : null });
                    return;
                }
            }
            if (ilist == null)
            {
                var lost = GetLost(o);
                ilist = new List<object>();
                lost.IList = ilist.AsReadOnly();
            }

            var count = (int)input.ReadVInt();
            var eArgs = new ReadArgs(tSrc.Collection1);
            for (int i = 0; i < count; i++)
            {
                var value = ReadImpl(eArgs);
                ilist.Add(value);
            }
        }
        void ReadCollectionT<T>(object o, TypeData tSrc, RuntimeType hint)
        {
            var l = o as ICollection<T>;
            List<object> ilist = null;
            List<object> GetIList()
            {
                if (ilist == null)
                {
                    ilist = new List<object>();
                    GetLost(o).IList = ilist.AsReadOnly();
                }
                return ilist;
            }
            var count = (int)input.ReadVInt();
            var eArgs = new ReadArgs(tSrc.Collection1, hint?.Collection1);
            for (int i = 0; i < count; i++)
            {
                var value = AsType(ReadImpl(eArgs));
                if (l != null && !l.IsReadOnly && value is T tValue)
                {
                    l.Add(tValue);
                }
                else
                {
                    GetIList().Add(value);
                }
            }
        }
        void ReadDict(object o, RuntimeType oType, TypeData tSrc)
        {
            var isRO = input.ReadBool();
            if (isRO)
                return;

            List<(object, object)> ilist = null;
            if (o is ObjectData od)
            {
                ilist = new List<(object, object)>();
                od.IDictionary = ilist.AsReadOnly();
            }
            else
            {
                var rtKey = tSrc.Collection1.RuntimeType();
                var rtValue = tSrc.Collection2.RuntimeType();
                bool overrides = false;
                if (oType.CollectionType == RuntimeCollectionType.IDictionaryKV)
                {
                    overrides = true;
                    rtKey = oType.Collection1;
                    rtValue = oType.Collection2;
                }
                if (rtKey != null && rtValue != null)
                {
                    var m = GetType().TryGetMethods(nameof(ReadDictKV), new[] { rtKey.Type, rtValue.Type }, typeof(object), typeof(TypeData), typeof(RuntimeType)).First();
                    m.Invoke(this, new object[] { o, tSrc, overrides ? oType : null });
                    return;
                }
            }
            if (ilist == null)
            {
                ilist = new List<(object, object)>();
                GetLost(o).IDictionary = ilist.AsReadOnly();
            }

            var count = (int)input.ReadVInt();
            var eKey = new ReadArgs(tSrc.Collection1);
            var eValue = new ReadArgs(tSrc.Collection2);
            for (int i = 0; i < count; i++)
            {
                var key = ReadImpl(eKey);
                var value = ReadImpl(eValue);
                ilist.Add((key, value));
            }
        }
        void ReadDictKV<K, V>(object o, TypeData tSrc, RuntimeType hint)
        {
            var d = o as IDictionary<K, V>;
            List<(object, object)> ilist = null;
            List<(object, object)> GetIList()
            {
                if (ilist == null)
                {
                    ilist = new List<(object, object)>();
                    GetLost(o).IDictionary = ilist.AsReadOnly();
                }
                return ilist;
            }
            var count = (int)input.ReadVInt();
            var eKey = new ReadArgs(tSrc.Collection1, hint?.Collection1);
            var eValue = new ReadArgs(tSrc.Collection2, hint?.Collection2);
            for (int i = 0; i < count; i++)
            {
                var key = AsType(ReadImpl(eKey));
                var value = AsType(ReadImpl(eValue));
                if (d != null && !d.IsReadOnly && key is K tKey && value is V tValue)
                {
                    d.Add(tKey, tValue);
                }
                else
                {
                    GetIList().Add((key, value));
                }
            }
        }
    }
}
