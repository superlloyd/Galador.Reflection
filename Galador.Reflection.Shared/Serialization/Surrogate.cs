using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Implement this interface to help the serializer read/write problematic types.
    /// i.e. type that are not described by their public properties and fields (such as <c>Bitmap</c>
    /// or <c>Stream</c>) and type which have readonly value type, such as <see cref="Tuple{T1}"/>.
    /// Do that by exposing the data as public property and fields, or implement a known collection.
    /// </summary>
    /// <typeparam name="T">The type that this interface will help read and write.</typeparam>
    public interface ISurrogate<T>
    {
        /// <summary>
        /// Initializes the surrogate with the a value
        /// </summary>
        /// <param name="value">The value that must be saved.</param>
        void Convert(T value);

        /// <summary>
        /// Return the value that was serialized.
        /// </summary>
        T Revert();

        /// <summary>
        /// apply deserialized data into an existing value
        /// </summary>
        void Populate(T value);
    }

#pragma warning disable 1591 // XML Comments

    /// <summary>
    /// Built in surrogate for <see cref="DBNull"/>.
    /// </summary>
    public class DBNullSurrogate : ISurrogate<DBNull>
    {
        void ISurrogate<DBNull>.Convert(DBNull value) { }

        public DBNull Revert() { return DBNull.Value; }

        void ISurrogate<DBNull>.Populate(DBNull value) => throw new NotSupportedException();
    }

    /// <summary>
    /// Built in surrogate for <see cref="DateTime"/>.
    /// </summary>
    public class DateTimeSurrogate : ISurrogate<DateTime>
    {
        void ISurrogate<DateTime>.Convert(DateTime value)
        {
            Ticks = value.Ticks;
            Kind = value.Kind;
        }

        DateTime ISurrogate<DateTime>.Revert()
        {
            return new DateTime(Ticks, Kind);
        }

        public long Ticks { get; set; }
        public DateTimeKind Kind { get; set; }

        void ISurrogate<DateTime>.Populate(DateTime value) => throw new NotSupportedException();
    }

    /// <summary>
    /// Built in surrogate for <see cref="DateTimeOffset"/>.
    /// </summary>
    public class DateTimeOffsetSurrogate : ISurrogate<DateTimeOffset>
    {
        void ISurrogate<DateTimeOffset>.Convert(DateTimeOffset value)
        {
            Ticks = value.Ticks;
            Offset = value.Offset.Ticks;
        }

        DateTimeOffset ISurrogate<DateTimeOffset>.Revert()
        {
            return new DateTimeOffset(Ticks, new TimeSpan(Offset));
        }

        public long Ticks { get; set; }
        public long Offset { get; set; }

        void ISurrogate<DateTimeOffset>.Populate(DateTimeOffset value) => throw new NotSupportedException();
    }

    /// <summary>
    /// Built in surrogate for <see cref="TimeSpan"/>.
    /// </summary>
    public class TimeSpanSurrogate : ISurrogate<TimeSpan>
    {
        void ISurrogate<TimeSpan>.Convert(TimeSpan value)
        {
            Ticks = value.Ticks;
        }

        TimeSpan ISurrogate<TimeSpan>.Revert()
        {
            return new TimeSpan(Ticks);
        }

        public long Ticks { get; set; }

        void ISurrogate<TimeSpan>.Populate(TimeSpan value) => throw new NotSupportedException();
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1}"/>.
    /// </summary>
    public class TupleSurrogate<T1> : ISurrogate<Tuple<T1>>, ISurrogate<ValueTuple<T1>>
    {
        void ISurrogate<Tuple<T1>>.Convert(Tuple<T1> value)
        {
            this.Item1 = value.Item1;
        }

        Tuple<T1> ISurrogate<Tuple<T1>>.Revert()
        {
            return Tuple.Create(Item1);
        }

        void ISurrogate<Tuple<T1>>.Populate(Tuple<T1> value) => throw new NotSupportedException();

        void ISurrogate<ValueTuple<T1>>.Convert(ValueTuple<T1> value)
        {
            this.Item1 = value.Item1;
        }

        ValueTuple<T1> ISurrogate<ValueTuple<T1>>.Revert()
        {
            return ValueTuple.Create(Item1);
        }

        void ISurrogate<ValueTuple<T1>>.Populate(ValueTuple<T1> value) => throw new NotSupportedException();

        public T1 Item1 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2}"/>, 
    /// <see cref="ValueTuple{T1, T2}"/>, <see cref="KeyValuePair{TKey, TValue}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2> : ISurrogate<Tuple<T1, T2>>, ISurrogate<ValueTuple<T1, T2>>, ISurrogate<KeyValuePair<T1, T2>>
    {
        void ISurrogate<Tuple<T1, T2>>.Convert(Tuple<T1, T2> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
        }

        Tuple<T1, T2> ISurrogate<Tuple<T1, T2>>.Revert()
        {
            return Tuple.Create(Item1, Item2);
        }

        void ISurrogate<Tuple<T1, T2>>.Populate(Tuple<T1, T2> value) => throw new NotSupportedException();

        void ISurrogate<ValueTuple<T1, T2>>.Convert(ValueTuple<T1, T2> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
        }

        ValueTuple<T1, T2> ISurrogate<ValueTuple<T1, T2>>.Revert()
        {
            return ValueTuple.Create(Item1, Item2);
        }

        void ISurrogate<ValueTuple<T1, T2>>.Populate(ValueTuple<T1, T2> value) => throw new NotSupportedException();

        void ISurrogate<KeyValuePair<T1, T2>>.Convert(KeyValuePair<T1, T2> value)
        {
            this.Item1 = value.Key;
            this.Item2 = value.Value;
        }

        KeyValuePair<T1, T2> ISurrogate<KeyValuePair<T1, T2>>.Revert()
        {
            return new KeyValuePair<T1, T2>(Item1, Item2);
        }

        void ISurrogate<KeyValuePair<T1, T2>>.Populate(KeyValuePair<T1, T2> value) => throw new NotSupportedException();

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2, T3}"/>, <see cref="ValueTuple{T1, T2, T3}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2, T3> : ISurrogate<Tuple<T1, T2, T3>>, ISurrogate<ValueTuple<T1, T2, T3>>
    {
        void ISurrogate<Tuple<T1, T2, T3>>.Convert(Tuple<T1, T2, T3> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
        }

        Tuple<T1, T2, T3> ISurrogate<Tuple<T1, T2, T3>>.Revert()
        {
            return Tuple.Create(Item1, Item2, Item3);
        }

        void ISurrogate<Tuple<T1, T2, T3>>.Populate(Tuple<T1, T2, T3> value) => throw new NotSupportedException();

        void ISurrogate<ValueTuple<T1, T2, T3>>.Convert(ValueTuple<T1, T2, T3> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
        }

        ValueTuple<T1, T2, T3> ISurrogate<ValueTuple<T1, T2, T3>>.Revert()
        {
            return ValueTuple.Create(Item1, Item2, Item3);
        }

        void ISurrogate<ValueTuple<T1, T2, T3>>.Populate(ValueTuple<T1, T2, T3> value) => throw new NotSupportedException();

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2, T3, T4}"/>, <see cref="ValueTuple{T1, T2, T3, T4}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2, T3, T4> : ISurrogate<Tuple<T1, T2, T3, T4>>, ISurrogate<ValueTuple<T1, T2, T3, T4>>
    {
        void ISurrogate<Tuple<T1, T2, T3, T4>>.Convert(Tuple<T1, T2, T3, T4> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
        }

        Tuple<T1, T2, T3, T4> ISurrogate<Tuple<T1, T2, T3, T4>>.Revert()
        {
            return Tuple.Create(Item1, Item2, Item3, Item4);
        }

        void ISurrogate<Tuple<T1, T2, T3, T4>>.Populate(Tuple<T1, T2, T3, T4> value) => throw new NotSupportedException();

        void ISurrogate<ValueTuple<T1, T2, T3, T4>>.Convert(ValueTuple<T1, T2, T3, T4> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
        }

        ValueTuple<T1, T2, T3, T4> ISurrogate<ValueTuple<T1, T2, T3, T4>>.Revert()
        {
            return ValueTuple.Create(Item1, Item2, Item3, Item4);
        }

        void ISurrogate<ValueTuple<T1, T2, T3, T4>>.Populate(ValueTuple<T1, T2, T3, T4> value) => throw new NotSupportedException();

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }
        public T4 Item4 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2, T3, T4, T5}"/>,<see cref="ValueTuple{T1, T2, T3, T4, T5}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2, T3, T4, T5> : ISurrogate<Tuple<T1, T2, T3, T4, T5>>, ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>
    {
        void ISurrogate<Tuple<T1, T2, T3, T4, T5>>.Convert(Tuple<T1, T2, T3, T4, T5> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
            this.Item5 = value.Item5;
        }

        Tuple<T1, T2, T3, T4, T5> ISurrogate<Tuple<T1, T2, T3, T4, T5>>.Revert()
        {
            return Tuple.Create(Item1, Item2, Item3, Item4, Item5);
        }

        void ISurrogate<Tuple<T1, T2, T3, T4, T5>>.Populate(Tuple<T1, T2, T3, T4, T5> value) => throw new NotSupportedException();

        void ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>.Convert(ValueTuple<T1, T2, T3, T4, T5> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
            this.Item5 = value.Item5;
        }

        ValueTuple<T1, T2, T3, T4, T5> ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>.Revert()
        {
            return ValueTuple.Create(Item1, Item2, Item3, Item4, Item5);
        }

        void ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>.Populate(ValueTuple<T1, T2, T3, T4, T5> value) => throw new NotSupportedException();

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }
        public T4 Item4 { get; set; }
        public T5 Item5 { get; set; }
    }

    /// <summary>
    /// class that avoid saving extraneous properties like Comparer while enabling unload existing instances
    /// </summary>
    //public class HasSetSurrogate<T> : ISurrogate<HashSet<T>>, ICollection<T>
    //{
    //    ICollection<T> collection = new List<T>();

    //    // ISurrogate

    //    public void Convert(HashSet<T> value) => collection = value;

    //    public HashSet<T> Revert() => new HashSet<T>(collection);

    //    public void Populate(HashSet<T> value)
    //    {
    //        foreach (var item in collection)
    //            value.Add(item);
    //    }

    //    // ICollection

    //    public int Count => collection.Count;

    //    public bool IsReadOnly => collection.IsReadOnly;

    //    public void Add(T item) => collection.Add(item);

    //    public void Clear() => collection.Clear();

    //    public bool Contains(T item) => collection.Contains(item);

    //    public void CopyTo(T[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);

    //    public IEnumerator<T> GetEnumerator() => collection.GetEnumerator();

    //    public bool Remove(T item) => collection.Remove(item);

    //    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    //}

    /// <summary>
    /// class that avoid saving extraneous properties like Comparer while enabling unload existing instances
    /// </summary>
    //public class DictionarySurrogate<TKey, TValue> : ISurrogate<Dictionary<TKey, TValue>>, ICollection<KeyValuePair<TKey, TValue>>
    //{
    //    ICollection<KeyValuePair<TKey, TValue>> collection = new List<KeyValuePair<TKey, TValue>>();

    //    // ISurrogate

    //    public void Convert(Dictionary<TKey, TValue> value) => collection = value;

    //    public Dictionary<TKey, TValue> Revert()
    //    {
    //        var result = new Dictionary<TKey, TValue>();
    //        Populate(result);
    //        return result;
    //    }

    //    public void Populate(Dictionary<TKey, TValue> value)
    //    {
    //        foreach (var item in collection)
    //            value[item.Key] = item.Value;
    //    }

    //    // ICollection

    //    public int Count => collection.Count;

    //    public bool IsReadOnly => collection.IsReadOnly;

    //    public void Add(KeyValuePair<TKey, TValue> item) => collection.Add(item);

    //    public void Clear() => collection.Clear();

    //    public bool Contains(KeyValuePair<TKey, TValue> item) => collection.Contains(item);

    //    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);

    //    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => collection.GetEnumerator();

    //    public bool Remove(KeyValuePair<TKey, TValue> item) => collection.Remove(item);

    //    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    //}

#pragma warning restore 1591 // XML Comments
}
