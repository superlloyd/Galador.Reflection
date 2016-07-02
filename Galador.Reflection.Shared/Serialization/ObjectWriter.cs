using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Galador.Reflection.Utils;
using System.Collections;
using SRS = System.Runtime.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// This write object to a version tolerant serialization stream. Very much like JSON with the 
    /// following relevant difference.
    /// 1. Class meta data is written, to avoid repeating property / field name. i.e. once a class has been written 
    /// only the property value need be written for each instance.
    /// 2. It supports IList AND IDictionary
    /// 3. Opaque type (such as Bitmap / Stream) or readonly type (such as Tuple) can still be serialized
    /// using ISurrogate<> classes.
    /// 4. It is a strongly typed serialization mechanism, however class name can be overridden using the
    /// Guid attribute on each class, effectively enabling replacing one class by another. Also class name 
    /// is not matched on assembly, only full type name.
    /// 5. Of course it also support various attribute to select which property fields are saved.
    /// </summary>
    public class ObjectWriter : IDisposable
    {
        public ObjectWriter(IPrimitiveWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            Writer = writer;
            Context = new ObjectContext();
            Writer.WriteVInt(VERSION);
        }
        public void Dispose() { Writer.Dispose(); }

        const ulong VERSION = 1;

        public ObjectContext Context { get; private set; }
        public IPrimitiveWriter Writer { get; private set; }

        /// <summary>
        /// DO NOT USE THAT, it will completely break the serialization stream if there a slight class change...
        /// ok for in process cloning though.
        /// </summary>
        public bool SkipMetaData { get; set; } = false;

        public void Write(object o)
        {
            Write(ReflectType.RObject, o);
        }

        // REMARK only cache surrogate for Well Known object ...
        internal void Write(ReflectType expected, object o)
        {
            // 1st write the ID of the object, return if already written
            if (expected.IsReference) 
            {
                ulong oid;
                var isKnown = Context.TryGetId(o, out oid);
                if (!isKnown)
                {
                    oid = Context.NewId();
                    Context.Register(oid, o);
                }
                Writer.WriteVInt(oid);
                if (isKnown)
                    return;
            }

            // 2nd write class info, **if needed**
            var ots = expected;
            if (!expected.IsFinal)
            {
                ots = ReflectType.GetType(o);
                Write(ReflectType.RReflectType, ots);
            }

            // finally write the item
            if (ots.IsIgnored)
            {
                // nothing!
            }
            else if (ots == ReflectType.RReflectType)
            {
                ((ReflectType)o).Write(this);
            }
            else if (ots == ReflectType.RType)
            {
                var rf = ReflectType.GetType((Type)o);
                Write(ReflectType.RReflectType, rf);
            }
            else if (ots.IsISerializable)
            {
                WriteISerializable(ots, o);
            }
            else if(ots.HasConverter)
            {
                WriteConverter(ots, o);
            }
            else if (ots.HasSurrogate)
            {
                BegingSurrogate(ots, o);
                try
                {
                    object o2;
                    if (!ots.TryGetSurrogate(o, out o2))
                        throw new InvalidOperationException("surrogate failure: couldn't get surrogate instance");
                    WriteObject(ots.Surrogate, o2);
                }
                finally { EndSurrogate(); }
            }
            else
            {
                WriteObject(ots, o);
            }
        }
        void BegingSurrogate(ReflectType ots, object o)
        {
            for (int i = 0; i < currentSurrogates.Count; i++)
            {
                var t = currentSurrogates[i];
                if (t.Item2 == o)
                    throw new ArgumentException($"Failed to write object({o}) with surrogate({ots.Surrogate}), as it reference itself.");
            }
            currentSurrogates.Add(Tuple.Create(ots, o));
        }
        void EndSurrogate()
        {
            currentSurrogates.RemoveAt(currentSurrogates.Count - 1);
        }
        List<Tuple<ReflectType, object>> currentSurrogates = new List<Tuple<ReflectType, object>>();

        void WriteISerializable(ReflectType ots, object o)
        {
            if (o is Missing)
            {
                var m = (Missing)o;
                Writer.WriteVInt(m.Collection.Count);
                foreach (var item in m.Collection)
                {
                    Write(ReflectType.RString, (string)item.Item1);
                    Write(ReflectType.RObject, item.Item2);
                }
            }
            else
            {
#if !__PCL__
                var serial = (SRS.ISerializable)o;
                var info = new SRS.SerializationInfo(typeof(object), new SRS.FormatterConverter());
                var ctx = new SRS.StreamingContext(SRS.StreamingContextStates.Persistence);
                serial.GetObjectData(info, ctx);
                Writer.WriteVInt(info.MemberCount);
                foreach (var item in info)
                {
                    Write(ReflectType.RString, item.Name);
                    Write(ReflectType.RObject, item.Value);
                }
#endif
            }
        }

        void WriteConverter(ReflectType ots, object value)
        {
            if (value is Missing)
            {
                var m = (Missing)value;
                Write(ReflectType.RString, m.ConverterString);
            }
            else
            {
#if !__PCL__
                var tc = ots.GetTypeConverter();
                if (tc == null)
                    throw new InvalidOperationException("Failed to get converter.");
                var s = tc.ConvertToInvariantString(value);
                Write(ReflectType.RString, s);
#endif
            }
        }

        void WriteObject(ReflectType ts, object o)
        {
            switch (ts.Kind)
            {
                default:
                case PrimitiveType.Object:
                    if (ts.IsArray)
                    {
                        var aa = (Array)o;
                        if (aa.Rank != ts.ArrayRank)
                            throw new ArgumentException($"{aa} Rank is {aa.Rank} instead of expected {ts.ArrayRank}");
                        for (int i = 0; i < ts.ArrayRank; i++)
                            Writer.WriteVInt(aa.GetLength(i));
                        foreach (var item in aa)
                            Write(ts.Element, item);
                    }
                    else if (ts.IsNullable)
                    {
                        if (o == null) Writer.Write(false);
                        else
                        {
                            Writer.Write(true);
                            Write(ts.GenericArguments[0], o);
                        }
                    }
                    else if (ts.IsEnum)
                    {
                        // TODO: Missing for Enum
                        WriteObject(ts.Element, o);
                    }
                    else
                    {
                        if (o is Missing)
                        {
                            var miss = (Missing)o;
                            foreach (var f in miss.Members)
                            {
                                Write(f.Type, f.Value);
                            }
                            switch (ts.CollectionType)
                            {
                                case ReflectCollectionType.IList:
                                case ReflectCollectionType.ICollectionT:
                                    Writer.WriteVInt(miss.Collection.Count);
                                    var coll = ts.Collection1 ?? ReflectType.RObject;
                                    foreach (var item in miss.Collection)
                                        Write(coll, item.Item1);
                                    break;
                                case ReflectCollectionType.IDictionary:
                                case ReflectCollectionType.IDictionaryKV:
                                    Writer.WriteVInt(miss.Collection.Count);
                                    var coll1 = ts.Collection1 ?? ReflectType.RObject;
                                    var coll2 = ts.Collection2 ?? ReflectType.RObject;
                                    foreach (var item in miss.Collection)
                                    {
                                        Write(coll1, item.Item1);
                                        Write(coll2, item.Item2);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            foreach (var f in ts.Members)
                            {
                                var p = f.GetValue(o);
                                Write(f.Type, p);
                            }
                            switch (ts.CollectionType)
                            {
                                case ReflectCollectionType.IList:
                                    WriteList((IList)o);
                                    break;
                                case ReflectCollectionType.IDictionary:
                                    WriteDict((IDictionary)o);
                                    break;
                                case ReflectCollectionType.ICollectionT:
                                    if (ts.listWrite == null)
                                        ts.listWrite = GetType().TryGetMethods("WriteCollection", new[] { ts.Collection1.Type }, ts.Type).FirstOrDefault();
                                    if (ts.listWrite != null)
                                        ts.listWrite.Invoke(this, new object[] { o });
                                    break;
                                case ReflectCollectionType.IDictionaryKV:
                                    if (ts.listWrite == null)
                                        ts.listWrite = GetType().TryGetMethods("WriteDictionary", new[] { ts.Collection1.Type, ts.Collection2.Type }, ts.Type).FirstOrDefault();
                                    if (ts.listWrite != null)
                                        ts.listWrite.Invoke(this, new object[] { o });
                                    break;
                            }
                        }
                    }
                    break;
                case PrimitiveType.String:
                    Writer.Write((string)o);
                    break;
                case PrimitiveType.Bytes:
                    Writer.Write((byte[])o);
                    break;
                case PrimitiveType.Guid:
                    Writer.Write((Guid)o);
                    break;
                case PrimitiveType.Bool:
                    Writer.Write((bool)o);
                    break;
                case PrimitiveType.Char:
                    Writer.Write((char)o);
                    break;
                case PrimitiveType.Byte:
                    Writer.Write((byte)o);
                    break;
                case PrimitiveType.SByte:
                    Writer.Write((sbyte)o);
                    break;
                case PrimitiveType.Int16:
                    Writer.Write((short)o);
                    break;
                case PrimitiveType.UInt16:
                    Writer.Write((ushort)o);
                    break;
                case PrimitiveType.Int32:
                    Writer.Write((int)o);
                    break;
                case PrimitiveType.UInt32:
                    Writer.Write((uint)o);
                    break;
                case PrimitiveType.Int64:
                    Writer.Write((long)o);
                    break;
                case PrimitiveType.UInt64:
                    Writer.Write((ulong)o);
                    break;
                case PrimitiveType.Single:
                    Writer.Write((float)o);
                    break;
                case PrimitiveType.Double:
                    Writer.Write((double)o);
                    break;
                case PrimitiveType.Decimal:
                    Writer.Write((decimal)o);
                    break;
            }
        }

        void WriteList(IList list)
        {
            var count = list.Count;
            Writer.WriteVInt(count);
            foreach (var item in list)
            {
                count--;
                Write(item);
            }
            if (count != 0)
                throw new ArgumentException($"({list}.Count reported an incorrect value ({list.Count})");
        }
        void WriteDict(IDictionary dictionary)
        {
            var count = dictionary.Count;
            Writer.WriteVInt(count);
            foreach (DictionaryEntry kv in dictionary)
            {
                count--;
                Write(kv.Key);
                Write(kv.Value);
            }
            if (count != 0)
                throw new ArgumentException($"({dictionary}.Count reported an incorrect value ({dictionary.Count})");
        }
        void WriteCollection<T>(ICollection<T> list)
        {
            var count = list.Count;
            Writer.WriteVInt(count);
            var surt = ReflectType.GetType(typeof(T));
            foreach (var item in list)
            {
                count--;
                Write(surt, item);
            }
            if (count != 0)
                throw new ArgumentException($"({list}.Count reported an incorrect value ({list.Count})");
        }
        void WriteDictionary<K, V>(IDictionary<K, V> list)
        {
            var count = list.Count;
            Writer.WriteVInt(count);
            var surk = ReflectType.GetType(typeof(K));
            var surv = ReflectType.GetType(typeof(V));
            foreach (var kv in list)
            {
                count--;
                Write(surk, kv.Key);
                Write(surv, kv.Value);
            }
            if (count != 0)
                throw new ArgumentException($"({list}.Count reported an incorrect value ({list.Count})");
        }
    }
}
