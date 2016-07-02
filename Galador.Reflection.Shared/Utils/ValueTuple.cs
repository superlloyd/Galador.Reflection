using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Utils
{
    public static class ValueTuple
    {
        public static ValueTuple<T1, T2> Create<T1, T2>(T1 t1, T2 t2) { return new ValueTuple<T1, T2> { Item1 = t1, Item2 = t2 }; }
        public static ValueTuple<T1, T2, T3> Create<T1, T2, T3>(T1 t1, T2 t2, T3 t3) { return new ValueTuple<T1, T2, T3> { Item1 = t1, Item2 = t2, Item3 = t3 }; }
        public static ValueTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 t1, T2 t2, T3 t3, T4 t4) { return new ValueTuple<T1, T2, T3, T4> { Item1 = t1, Item2 = t2, Item3 = t3, Item4 = t4 }; }
        public static ValueTuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5) { return new ValueTuple<T1, T2, T3, T4, T5> { Item1 = t1, Item2 = t2, Item3 = t3, Item4 = t4, Item5 = t5 }; }
    }

    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
        public override string ToString() { return $"({Item1}, {Item2})"; }
    }
    public struct ValueTuple<T1, T2, T3>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public override string ToString() { return $"({Item1}, {Item2}, {Item3})"; }
    }
    public struct ValueTuple<T1, T2, T3, T4>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public override string ToString() { return $"({Item1}, {Item2}, {Item3}, {Item4})"; }
    }
    public struct ValueTuple<T1, T2, T3, T4, T5>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;
        public T5 Item5;
        public override string ToString() { return $"({Item1}, {Item2}, {Item3}, {Item4}, {Item5})"; }
    }
}
