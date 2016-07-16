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
    /// This write objects to a version tolerant serialization stream. Very much like JSON writing public field
    /// and property and matching them by name. WIth the following differences:
    /// <br/>
    /// 1. Class meta data is written, to avoid repeating property / field name. i.e. once a class has been written 
    /// only the property value need be written for each instance.
    /// <br/>
    /// 2. Object 'ID' are  created and written for each reference, to avoid circular reference.
    /// <br/>
    /// 3. It supports <see cref="IList"/>, <see cref="ICollection{T}"/>, 
    /// <see cref="IDictionary"/>, <see cref="IDictionary{TKey, TValue}"/>
    /// <br/>
    /// 4. It supports <see cref="System.ComponentModel.TypeConverter"/>, <see cref="System.Runtime.Serialization.ISerializable"/>.
    /// <br/>
    /// 4. Opaque type (such as <c>Bitmap</c> or <see cref="System.IO.Stream"/>) or readonly type (such as Tuple) 
    /// can still be serialized using <see cref="ISurrogate{T}"/> classes.
    /// <br/>
    /// 5. It is a strongly typed serialization mechanism, however class name and assembly name
    /// can be overridden using the <see cref="SerializationNameAttribute"/> on each class, 
    /// effectively enabling replacing one class by another. Also class name 
    /// is not matched on assembly, only full type name.
    /// <br/>
    /// 6. Of course it also support various attribute to select which property fields are saved.
    /// </summary>
    public class ObjectWriter : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectWriter"/> class.
        /// </summary>
        /// <param name="writer">The writer to which the data is written.</param>
        /// <exception cref="System.ArgumentNullException">If the writer is null</exception>
        public ObjectWriter(IPrimitiveWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            Writer = writer;
            Context = new ObjectContext();
            Writer.WriteVInt(VERSION);
        }
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose() { Writer.Dispose(); }

        // v2 added BaseClass, IsSurrogateType
        const ulong VERSION = 1;

        /// <summary>
        /// this writer's context. That will hold reference of all objects written.
        /// </summary>
        public ObjectContext Context { get; private set; }
        /// <summary>
        /// The data stream to which object are written.
        /// </summary>
        public IPrimitiveWriter Writer { get; private set; }

        /// <summary>
        /// Some settings that can affect the serialization process.
        /// </summary>
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
        /// Writes the next object to the <see cref="Writer"/>.
        /// </summary>
        /// <param name="o">The object to write.</param>
        public void Write(object o)
        {
            if (recurseDepth++ == 0)
            {
                var sFlags = Settings.ToFlags();
                Writer.WriteVInt(sFlags);
            }
            try { Write(ReflectType.RObject, o); }
            finally { recurseDepth--; }
        }
        int recurseDepth = 0;

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
            else if (ots.IsISerializable && !settings.IgnoreISerializable)
            {
                WriteISerializable(ots, o);
            }
            else if (ots.HasConverter && !settings.IgnoreTypeConverter)
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
#if __NETCORE__
                throw new PlatformNotSupportedException(".NETCore + ISerializable");
#elif !__PCL__
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
                            var colt = ts.CollectionInterface;
                            switch (colt.CollectionType)
                            {
                                case ReflectCollectionType.IList:
                                case ReflectCollectionType.ICollectionT:
                                    Writer.WriteVInt(miss.Collection.Count);
                                    var coll = colt.Collection1 ?? ReflectType.RObject;
                                    foreach (var item in miss.Collection)
                                        Write(coll, item.Item1);
                                    break;
                                case ReflectCollectionType.IDictionary:
                                case ReflectCollectionType.IDictionaryKV:
                                    Writer.WriteVInt(miss.Collection.Count);
                                    var coll1 = colt.Collection1 ?? ReflectType.RObject;
                                    var coll2 = colt.Collection2 ?? ReflectType.RObject;
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
                            foreach (var m in ts.RuntimeMembers)
                            {
                                var p = m.RuntimeMember.GetValue(o);
                                Write(m.Type, p);
                            }

                            var colt = ts.CollectionInterface;
                            switch (colt.CollectionType)
                            {
                                case ReflectCollectionType.IList:
                                    WriteList((IList)o);
                                    break;
                                case ReflectCollectionType.IDictionary:
                                    WriteDict((IDictionary)o);
                                    break;
                                case ReflectCollectionType.ICollectionT:
                                    if (colt.listWrite == null)
                                        colt.listWrite = FastMethod.GetMethod(GetType().TryGetMethods("WriteCollection", new[] { colt.Collection1.Type }, ts.Type).First());
                                    if (colt.listWrite != null)
                                        colt.listWrite.Invoke(this, o);
                                    break;
                                case ReflectCollectionType.IDictionaryKV:
                                    if (colt.listWrite == null)
                                        colt.listWrite = FastMethod.GetMethod(GetType().TryGetMethods("WriteDictionary", new[] { colt.Collection1.Type, colt.Collection2.Type }, ts.Type).First());
                                    if (colt.listWrite != null)
                                        colt.listWrite.Invoke(this, o);
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
            Writer.Write(list.IsReadOnly);
            if (list.IsReadOnly)
                return;
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
            Writer.Write(dictionary.IsReadOnly);
            if (dictionary.IsReadOnly)
                return;
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
            Writer.Write(list.IsReadOnly);
            if (list.IsReadOnly)
                return;
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
            Writer.Write(list.IsReadOnly);
            if (list.IsReadOnly)
                return;
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
