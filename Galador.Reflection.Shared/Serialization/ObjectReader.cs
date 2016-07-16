using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Galador.Reflection.Utils;
using System.IO;
using System.Collections;
using SRS = System.Runtime.Serialization;
using System.Runtime.CompilerServices;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// This class will deserialize an object (or many object) from a given <see cref="IPrimitiveReader"/>.
    /// </summary>
    /// <seealso cref="ObjectWriter"/>
    public class ObjectReader : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectReader"/> class.
        /// </summary>
        /// <param name="reader">The reader from which data is read.</param>
        /// <exception cref="System.ArgumentNullException">If the reader is null</exception>
        /// <exception cref="System.ArgumentException">If the data stream is a higher version number that this reader.</exception>
        public ObjectReader(IPrimitiveReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            Reader = reader;
            Context = new ObjectContext();
            VERSION = Reader.ReadVUInt();
            switch (VERSION)
            {
                case 1:
                    break;
                default:
                    throw new ArgumentException("Unknown version number " + VERSION);
            }
        }
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose() { Reader.Dispose(); }

        internal ulong VERSION;


        /// <summary>
        /// this reader's context. That will hold reference of all objects read.
        /// </summary>
        public ObjectContext Context { get; private set; }
        /// <summary>
        /// The data stream from which object are read.
        /// </summary>
        public IPrimitiveReader Reader { get; private set; }

        /// <summary>
        /// Whether or not all data was read successfully. If any data was not successfully used, will be set to false.
        /// </summary>
        public bool Success { get; private set; } = true;

        public SerializationSettings Settings
        {
            get
            {
                if (settings == null)
                    settings = new SerializationSettings();
                return settings;
            }
            set { settings = value; }
        }
        SerializationSettings settings;

        /// <summary>
        /// Reads the next object from the <see cref="Reader"/>.
        /// </summary>
        /// <returns>The next object in the stream.</returns>
        public object Read()
        {
            recurseDepth++;
            try
            {
                return Read(ReflectType.RObject, null);
            }
            finally
            {
                recurseDepth--;
                if (recurseDepth == 0)
                {
                    foreach (var item in Context.Objects.OfType<IDeserialized>())
                        item.Deserialized();
#if !__PCL__ && !__NETCORE__
                    foreach (var item in Context.Objects.OfType<SRS.IDeserializationCallback>())
                        item.OnDeserialization(this.Context);
#endif
                }
            }
        }
        int recurseDepth = 0;

        internal object Read(ReflectType expected, object possibleValue)
        {
            object result;
            ulong oid = 0;
            if (expected.IsReference)
            {
                oid = Reader.ReadVUInt();
                if (Context.TryGetObject(oid, out result))
                    return result;
            }

            var actual = expected;
            if (!expected.IsFinal) // if expected type is not final then write actual type
            {
                actual = (ReflectType)Read(ReflectType.RReflectType, null);
            }

            object o = null;
            if (actual.IsIgnored)
            {
                o = null;
                if (oid > 0)
                    Context.Register(oid, null);
            }
            else if (actual == ReflectType.RReflectType)
            {
                var rt = new ReflectType();
                Context.Register(oid, rt);
                o = rt;
                rt.Read(this);
            }
            else if (actual == ReflectType.RType)
            {
                var rf = (ReflectType)Read(ReflectType.RReflectType, null);
                o = rf.Type;
                Context.Register(oid, o);
            }
            else if (actual.IsISerializable && !settings.IgnoreISerializable)
            {
                o = ReadISerializable(actual, oid, possibleValue);
            }
            else if (actual.HasConverter && !settings.IgnoreISerializable)
            {
                o = ReadConverter(actual, oid);
            }
            else if (actual.HasSurrogate)
            {
                o = ReadObject(actual.Surrogate, 0, null);
                object o2 = o;
                if (!(o is Missing) && !actual.TryGetOriginal(o, out o2))
                    throw new InvalidOperationException("surrogate failure: couldn't get normal instance");
                if (oid != 0)
                    Context.Register(oid, o2);
                o = o2;
            }
            else
            {
                o = ReadObject(actual, oid, possibleValue);
            }
            // since value types are not saved in the context, awake them now!
            if (oid == 0)
            {
                if (o is IDeserialized)
                    ((IDeserialized)o).Deserialized();
#if !__PCL__ && !__NETCORE__
                if (o is SRS.IDeserializationCallback)
                    ((SRS.IDeserializationCallback)o).OnDeserialization(null);
#endif
            }
            return o;
        }

        object ReadISerializable(ReflectType ts, ulong oid, object possibleValue)
        {
#if __PCL__
            throw new PlatformNotSupportedException("PCL");
#elif __NETCORE__
            var missing = new Missing(ts);
            var list = new List<Tuple<object, object>>();
            missing.Collection = list;
            var N = Reader.ReadVInt();
            for (int i = 0; i < N; i++)
            {
                var s = (string)Read(ReflectType.RString, null);
                var o = Read(ReflectType.RObject, null);
                list.Add(Tuple.Create<object, object>(s, o));
            }
            if (ts.Type != null)
            {
                return possibleValue;
            }
            return missing;
#else
            var info = new SRS.SerializationInfo(typeof(object), new SRS.FormatterConverter());
            var ctx = new SRS.StreamingContext(SRS.StreamingContextStates.Persistence);
            var N = (int)Reader.ReadVInt();
            for (int i = 0; i < N; i++)
            {
                var s = (string)Read(ReflectType.RString, null);
                var o = Read(ReflectType.RObject, null);
                info.AddValue(s, o);
            }
            if (ts.Type != null)
            {
                if (possibleValue != null && ts.Type.IsInstanceOf(possibleValue))
                {
                    var ctor = ts.Type.TryGetConstructors(info.GetType(), ctx.GetType()).FirstOrDefault();
                    if (ctor != null)
                    {
                        // No FastMethod() couldn't manage to call constructor on existing instance
                        //var fctor = new FastMethod(ctor, true);
                        //fctor.Invoke(possibleValue, info, ctx);
                        ctor.Invoke(possibleValue, new object[] { info, ctx }); // Dare to do it! Call constructor on existing instance!!
                        if (oid > 0)
                            Context.Register(oid, possibleValue);
                        return possibleValue;
                    }
                }
                else
                {
                    var o = ts.Type.TryConstruct(info, ctx) ?? ts.FastType.TryConstruct();
                    if (oid > 0)
                        Context.Register(oid, o);
                    return o;
                }
            }
            var missing = new Missing(ts);
            if (oid > 0)
                Context.Register(oid, missing);
            var list = new List<Tuple<object, object>>();
            missing.Collection = list;
            foreach (var kv in info)
                list.Add(Tuple.Create<object, object>(kv.Name, kv.Value));
            return missing;
#endif
        }

        object ReadConverter(ReflectType ts, ulong oid)
        {
#if __PCL__
            throw new PlatformNotSupportedException("PCL");
#else
            var s = (string)Read(ReflectType.RString, null);
            var tc = ts.GetTypeConverter();
            if (tc != null)
            {
                var o = tc.ConvertFromInvariantString(s);
                if (oid > 0)
                    Context.Register(oid, o);
                return o;
            }
            var missing = new Missing(ts);
            if (oid > 0)
                Context.Register(oid, missing);
            missing.ConverterString = s;
            return missing;
#endif
        }

        static bool Inc(int[] indices, int[] rank)
        {
            for (int i = rank.Length - 1; i >= 0; i--)
            {
                indices[i]++;
                if (indices[i] < rank[i])
                    return true;
                indices[i] = 0;
            }
            return false;
        }
        object ReadObject(ReflectType ts, ulong oid, object possibleValue)
        {
            Func<object, object> RETURN_REGISTER = (value) =>
            {
                // register everything, value types might have been written as objects!
                if (oid > 0)
                    Context.Register(oid, value);
                return value;
            };
            switch (ts.Kind)
            {
                default:
                case PrimitiveType.Object:
                    {
                        if (ts.IsArray)
                        {
                            var ranks = Enumerable.Range(0, ts.ArrayRank).Select(x => (int)Reader.ReadVInt()).ToArray();
                            var array = Array.CreateInstance(ts.Element.Type ?? typeof(Missing), ranks);
                            if (oid != 0)
                                Context.Register(oid, array);
                            if (ranks.All(x => x > 0))
                            {
                                var indices = new int[ranks.Length];
                                do
                                {
                                    var value = Read(ts.Element, null);
                                    array.SetValue(value, indices);
                                }
                                while (Inc(indices, ranks));
                            }
                            return array;
                        }
                        else if (ts.IsNullable)
                        {
                            object o = null;
                            var isThere = Reader.ReadBool();
                            if (isThere)
                                o = Read(ts.GenericArguments[0], null);
                            if (oid != 0)
                                Context.Register(oid, o);
                            return o;
                        }
                        else if (ts.IsEnum)
                        {
                            var eoVal = Read(ts.Element, null);
                            var eVal = ts.Type != null ? Enum.ToObject(ts.Type, eoVal) : eoVal;
                            if (oid != 0)
                                Context.Register(oid, eVal);
                            return eVal;
                        }
                        else
                        {
                            object o = null;
                            if (ts.Type == null)
                            {
                                var missing = new Missing(ts);
                                o = missing;
                                if (oid != 0)
                                    Context.Register(oid, missing);
                                foreach (var p in ts.Members)
                                {
                                    var value = Read(p.Type, null);
                                    missing.Members[p.Name].Value = value;
                                }
                                var colt = ts.CollectionInterface;
                                switch (colt.CollectionType)
                                {
                                    case ReflectCollectionType.IList:
                                    case ReflectCollectionType.ICollectionT:
                                        {
                                            var list = new List<Tuple<object, object>>();
                                            missing.Collection = list;
                                            var N = (int)Reader.ReadVInt();
                                            var coll = colt.Collection1 ?? ReflectType.RObject;
                                            for (int i = 0; i < N; i++)
                                            {
                                                var value = Read(coll, null);
                                                list.Add(Tuple.Create<object, object>(value, null));
                                            }
                                        }
                                        break;
                                    case ReflectCollectionType.IDictionary:
                                    case ReflectCollectionType.IDictionaryKV:
                                        {
                                            var list = new List<Tuple<object, object>>();
                                            missing.Collection = list;
                                            var N = (int)Reader.ReadVInt();
                                            var coll1 = colt.Collection1 ?? ReflectType.RObject;
                                            var coll2 = colt.Collection2 ?? ReflectType.RObject;
                                            for (int i = 0; i < N; i++)
                                            {
                                                var key = Read(coll1, null);
                                                var value = Read(coll2, null);
                                                list.Add(Tuple.Create<object, object>(key, value));
                                            }
                                        }
                                        break;
                                }
                                return o;
                            }
                            else
                            {
                                o = possibleValue ?? ts.FastType.TryConstruct();
                                if (oid != 0)
                                    Context.Register(oid, o);
                                foreach (var m in ts.RuntimeMembers)
                                {
                                    if (m.RuntimeMember == null || !m.RuntimeMember.TryFastReadSet(this, o))
                                    {
                                        object org = null;
                                        if (m.Type.IsReference)
                                            org = m.RuntimeMember?.GetValue(o);
                                        var value = Read(m.Type, org);
                                        m.RuntimeMember?.SetValue(o, value);
                                    }
                                }

                                var colt = ts.CollectionInterface;
                                switch (colt.CollectionType)
                                {
                                    case ReflectCollectionType.IList:
                                        ReadList((IList)o);
                                        break;
                                    case ReflectCollectionType.IDictionary:
                                        ReadDict((IDictionary)o);
                                        break;
                                    // REMARK do not use FastMethod or make sure it is cached (as it is expensive to create)
                                    case ReflectCollectionType.ICollectionT:
                                        if (colt.listRead == null)
                                            colt.listRead = GetType().TryGetMethods("ReadCollectionT", new[] { colt.Collection1.Type }, ts.Type, typeof(ReflectType)).First();
                                        if (colt.listRead != null)
                                            colt.listRead.Invoke(this, new object[] { o, colt.Collection1 });
                                        break;
                                    case ReflectCollectionType.IDictionaryKV:
                                        if (colt.listRead == null)
                                            colt.listRead = GetType().TryGetMethods("ReadDictKV", new[] { colt.Collection1.Type, colt.Collection2.Type }, ts.Type, typeof(ReflectType), typeof(ReflectType)).First();
                                        if (colt.listRead != null)
                                            colt.listRead.Invoke(this, new object[] { o, colt.Collection1, colt.Collection2 });
                                        break;
                                }
                            }
                            return o;
                        }
                    }
                case PrimitiveType.String:
                    return RETURN_REGISTER(this.Reader.ReadString());
                case PrimitiveType.Bytes:
                    return RETURN_REGISTER(this.Reader.ReadBytes());
                case PrimitiveType.Guid:
                    return RETURN_REGISTER(this.Reader.ReadGuid());
                case PrimitiveType.Bool:
                    return RETURN_REGISTER(this.Reader.ReadBool());
                case PrimitiveType.Char:
                    return RETURN_REGISTER(this.Reader.ReadChar());
                case PrimitiveType.Byte:
                    return RETURN_REGISTER(this.Reader.ReadByte());
                case PrimitiveType.SByte:
                    return RETURN_REGISTER(this.Reader.ReadSByte());
                case PrimitiveType.Int16:
                    return RETURN_REGISTER(this.Reader.ReadInt16());
                case PrimitiveType.UInt16:
                    return RETURN_REGISTER(this.Reader.ReadUInt16());
                case PrimitiveType.Int32:
                    return RETURN_REGISTER(this.Reader.ReadInt32());
                case PrimitiveType.UInt32:
                    return RETURN_REGISTER(this.Reader.ReadUInt32());
                case PrimitiveType.Int64:
                    return RETURN_REGISTER(this.Reader.ReadInt64());
                case PrimitiveType.UInt64:
                    return RETURN_REGISTER(this.Reader.ReadUInt64());
                case PrimitiveType.Single:
                    return RETURN_REGISTER(this.Reader.ReadSingle());
                case PrimitiveType.Double:
                    return RETURN_REGISTER(this.Reader.ReadDouble());
                case PrimitiveType.Decimal:
                    return RETURN_REGISTER(this.Reader.ReadDecimal());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadList(IList o)
        {
            var isRO = Reader.ReadBool();
            if (isRO)
                return;
            var count = (int)Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var value = Read();
                if (o != null)
                    o.Add(value);
            }
        }
        void ReadDict(IDictionary o)
        {
            var isRO = Reader.ReadBool();
            if (isRO)
                return;
            var count = (int)Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var key = Read();
                var value = Read();
                if (o != null)
                    o.Add(key, value);
            }
        }
        void ReadCollectionT<T>(ICollection<T> col, ReflectType tT)
        {
            var isRO = Reader.ReadBool();
            if (isRO)
                return;
            var count = (int)Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var value = Read(tT, null);
                if (value is T && col != null)
                    col.Add((T)value);
            }
        }
        void ReadDictKV<K, V>(IDictionary<K, V> dict, ReflectType tKey, ReflectType tVal)
        {
            var isRO = Reader.ReadBool();
            if (isRO)
                return;
            var count = (int)Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var key = Read(tKey, null);
                var value = Read(tVal, null);
                if (key is K && value is V && dict != null)
                    dict.Add((K)key, (V)value);
            }
        }
    }
}
