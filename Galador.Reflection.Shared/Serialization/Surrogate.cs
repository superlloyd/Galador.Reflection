using Galador.Reflection.Utils;
using System;
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
        void Initialize(T value);
        /// <summary>
        /// Return the value that was serialized.
        /// </summary>
        T Instantiate();
    }

#pragma warning disable 1591 // XML Comments

    /// <summary>
    /// Built in surrogate for <see cref="DBNull"/>.
    /// </summary>
    public class DBNullSurrogate : ISurrogate<DBNull>
    {
        void ISurrogate<DBNull>.Initialize(DBNull value) { }

        public DBNull Instantiate() { return DBNull.Value; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="DateTime"/>.
    /// </summary>
    public class DateTimeSurrogate : ISurrogate<DateTime>
    {
        void ISurrogate<DateTime>.Initialize(DateTime value)
        {
            Ticks = value.Ticks;
            Kind = value.Kind;
        }

        DateTime ISurrogate<DateTime>.Instantiate()
        {
            return new DateTime(Ticks, Kind);
        }

        public long Ticks { get; set; }
        public DateTimeKind Kind { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="DateTimeOffset"/>.
    /// </summary>
    public class DateTimeOffsetSurrogate : ISurrogate<DateTimeOffset>
    {
        void ISurrogate<DateTimeOffset>.Initialize(DateTimeOffset value)
        {
            Ticks = value.Ticks;
            Offset = value.Offset.Ticks;
        }

        DateTimeOffset ISurrogate<DateTimeOffset>.Instantiate()
        {
            return new DateTimeOffset(Ticks, new TimeSpan(Offset));
        }

        public long Ticks { get; set; }
        public long Offset { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="TimeSpan"/>.
    /// </summary>
    public class TimeSpanSurrogate : ISurrogate<TimeSpan>
    {
        void ISurrogate<TimeSpan>.Initialize(TimeSpan value)
        {
            Ticks = value.Ticks;
        }

        TimeSpan ISurrogate<TimeSpan>.Instantiate()
        {
            return new TimeSpan(Ticks);
        }

        public long Ticks { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1}"/>.
    /// </summary>
    public class TupleSurrogate<T1> : ISurrogate<Tuple<T1>>
    {
        void ISurrogate<Tuple<T1>>.Initialize(Tuple<T1> value)
        {
            this.Item1 = value.Item1;
        }

        Tuple<T1> ISurrogate<Tuple<T1>>.Instantiate()
        {
            return Tuple.Create(Item1);
        }

        public T1 Item1 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2}"/>, 
    /// <see cref="ValueTuple{T1, T2}"/>, <see cref="KeyValuePair{TKey, TValue}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2> : ISurrogate<Tuple<T1, T2>>, ISurrogate<ValueTuple<T1, T2>>, ISurrogate<KeyValuePair<T1, T2>>
    {
        void ISurrogate<Tuple<T1, T2>>.Initialize(Tuple<T1, T2> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
        }

        Tuple<T1, T2> ISurrogate<Tuple<T1, T2>>.Instantiate()
        {
            return Tuple.Create(Item1, Item2);
        }

        void ISurrogate<ValueTuple<T1, T2>>.Initialize(ValueTuple<T1, T2> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
        }

        ValueTuple<T1, T2> ISurrogate<ValueTuple<T1, T2>>.Instantiate()
        {
            return ValueTuple.Create(Item1, Item2);
        }

        void ISurrogate<KeyValuePair<T1, T2>>.Initialize(KeyValuePair<T1, T2> value)
        {
            this.Item1 = value.Key;
            this.Item2 = value.Value;
        }

        KeyValuePair<T1, T2> ISurrogate<KeyValuePair<T1, T2>>.Instantiate()
        {
            return new KeyValuePair<T1, T2>(Item1, Item2);
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2, T3}"/>, <see cref="ValueTuple{T1, T2, T3}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2, T3> : ISurrogate<Tuple<T1, T2, T3>>, ISurrogate<ValueTuple<T1, T2, T3>>
    {
        void ISurrogate<Tuple<T1, T2, T3>>.Initialize(Tuple<T1, T2, T3> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
        }

        Tuple<T1, T2, T3> ISurrogate<Tuple<T1, T2, T3>>.Instantiate()
        {
            return Tuple.Create(Item1, Item2, Item3);
        }

        void ISurrogate<ValueTuple<T1, T2, T3>>.Initialize(ValueTuple<T1, T2, T3> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
        }

        ValueTuple<T1, T2, T3> ISurrogate<ValueTuple<T1, T2, T3>>.Instantiate()
        {
            return ValueTuple.Create(Item1, Item2, Item3);
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }
    }

    /// <summary>
    /// Built in surrogate for <see cref="Tuple{T1, T2, T3, T4}"/>, <see cref="ValueTuple{T1, T2, T3, T4}"/>.
    /// </summary>
    public class TupleSurrogate<T1, T2, T3, T4> : ISurrogate<Tuple<T1, T2, T3, T4>>, ISurrogate<ValueTuple<T1, T2, T3, T4>>
    {
        void ISurrogate<Tuple<T1, T2, T3, T4>>.Initialize(Tuple<T1, T2, T3, T4> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
        }

        Tuple<T1, T2, T3, T4> ISurrogate<Tuple<T1, T2, T3, T4>>.Instantiate()
        {
            return Tuple.Create(Item1, Item2, Item3, Item4);
        }

        void ISurrogate<ValueTuple<T1, T2, T3, T4>>.Initialize(ValueTuple<T1, T2, T3, T4> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
        }

        ValueTuple<T1, T2, T3, T4> ISurrogate<ValueTuple<T1, T2, T3, T4>>.Instantiate()
        {
            return ValueTuple.Create(Item1, Item2, Item3, Item4);
        }

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
        void ISurrogate<Tuple<T1, T2, T3, T4, T5>>.Initialize(Tuple<T1, T2, T3, T4, T5> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
            this.Item5 = value.Item5;
        }

        Tuple<T1, T2, T3, T4, T5> ISurrogate<Tuple<T1, T2, T3, T4, T5>>.Instantiate()
        {
            return Tuple.Create(Item1, Item2, Item3, Item4, Item5);
        }

        void ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>.Initialize(ValueTuple<T1, T2, T3, T4, T5> value)
        {
            this.Item1 = value.Item1;
            this.Item2 = value.Item2;
            this.Item3 = value.Item3;
            this.Item4 = value.Item4;
            this.Item5 = value.Item5;
        }

        ValueTuple<T1, T2, T3, T4, T5> ISurrogate<ValueTuple<T1, T2, T3, T4, T5>>.Instantiate()
        {
            return ValueTuple.Create(Item1, Item2, Item3, Item4, Item5);
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }
        public T4 Item4 { get; set; }
        public T5 Item5 { get; set; }
    }

#pragma warning restore 1591 // XML Comments
}
