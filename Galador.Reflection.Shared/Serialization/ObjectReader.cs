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
    public class ObjectReader : IDisposable
    {
        public ObjectReader(IPrimitiveReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            Reader = reader;
            Context = new ObjectContext();
            VERSION = Reader.ReadVUInt();
            switch (VERSION)
            {
                case 1: break;
                default:
                    throw new ArgumentException("Unknown version number " + VERSION);
            }
        }
        public void Dispose() { Reader.Dispose(); }

        ulong VERSION;

        public ObjectContext Context { get; private set; }
        public IPrimitiveReader Reader { get; private set; }

        /// <summary>
        /// DO NOT USE THAT, it will completely break the serialization stream if there a slight class change...
        /// ok for in process cloning though.
        /// </summary>
        public bool SkipMetaData { get; set; } = false;

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
#if !__PCL__
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
            if (actual == ReflectType.RReflectType)
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
            else if (actual.IsISerializable)
            {
                o = ReadISerializable(actual, oid, possibleValue);
            }
            else if (actual.HasConverter)
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
#if !__PCL__
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
#else
            var info = new SRS.SerializationInfo(typeof(object), new SRS.FormatterConverter());
            var ctx = new SRS.StreamingContext(SRS.StreamingContextStates.Persistence);
            var N = Reader.ReadVInt();
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
                        ctor.Invoke(possibleValue, new object[] { info, ctx }); // Dare to do it! Call constructor on existing instance!!
                        if (oid > 0)
                            Context.Register(oid, possibleValue);
                        return possibleValue;
                    }
                }
                else
                {
                    var o = ts.Type.TryConstruct(info, ctx)
                        ?? ts.Type.TryConstruct() // try empty object then?
                        ?? ts.Type.GetUninitializedObject(); // last resort
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
                                switch (ts.CollectionType)
                                {
                                    case ReflectCollectionType.IList:
                                    case ReflectCollectionType.ICollectionT:
                                        {
                                            var list = new List<Tuple<object, object>>();
                                            missing.Collection = list;
                                            var N = (int)Reader.ReadVInt();
                                            var coll = ts.Collection1 ?? ReflectType.RObject;
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
                                            var coll1 = ts.Collection1 ?? ReflectType.RObject;
                                            var coll2 = ts.Collection2 ?? ReflectType.RObject;
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
                                o = possibleValue ?? ts.Type.TryConstruct() ?? ts.Type.GetUninitializedObject();
                                if (oid != 0)
                                    Context.Register(oid, o);
                                foreach (var p in ts.Members)
                                {
                                    var org = p.GetValue(o);
                                    var value = Read(p.Type, org);
                                    p.SetValue(o, value);
                                }
                                switch (ts.CollectionType)
                                {
                                    case ReflectCollectionType.IList:
                                        ReadList((IList)o);
                                        break;
                                    case ReflectCollectionType.IDictionary:
                                        ReadDict((IDictionary)o);
                                        break;
                                    case ReflectCollectionType.ICollectionT:
                                        if (ts.listRead == null)
                                            ts.listRead = GetType().TryGetMethods("ReadCollectionT", new[] { ts.Collection1.Type }, ts.Type, typeof(ReflectType)).FirstOrDefault();
                                        if (ts.listRead != null)
                                            ts.listRead.Invoke(this, new object[] { o, ts.Collection1 });
                                        break;
                                    case ReflectCollectionType.IDictionaryKV:
                                        if (ts.listWrite == null)
                                            ts.listWrite = GetType().TryGetMethods("ReadDictKV", new[] { ts.Collection1.Type, ts.Collection2.Type }, ts.Type, typeof(ReflectType), typeof(ReflectType)).FirstOrDefault();
                                        if (ts.listWrite != null)
                                            ts.listWrite.Invoke(this, new object[] { o, ts.Collection1, ts.Collection2 });
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
            var count = Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var value = Read();
                o.Add(value);
            }
        }
        void ReadDict(IDictionary o)
        {
            var count = Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var key = Read();
                var value = Read();
                o.Add(key, value);
            }
        }
        void ReadCollectionT<T>(ICollection<T> col, ReflectType tT)
        {
            var count = Reader.ReadVInt();
            var typeT = typeof(T);
            for (int i = 0; i < count; i++)
            {
                var value = Read(tT, null);
                if (value is T)
                    col.Add((T)value);
            }
        }
        void ReadDictKV<K, V>(IDictionary<K, V> dict, ReflectType tKey, ReflectType tVal)
        {
            var count = Reader.ReadVInt();
            for (int i = 0; i < count; i++)
            {
                var key = Read(tKey, null);
                var value = Read(tVal, null);
                if (key is K && value is V) 
                    dict.Add((K)key, (V)value);
            }
        }
    }
}
