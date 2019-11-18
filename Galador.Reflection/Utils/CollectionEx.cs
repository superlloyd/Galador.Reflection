using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Static utility classes for collection
    /// </summary>
    static class CollectionEx
    {
        public static byte[] SubArray(this byte[] data, int start, int count)
        {
            var res = new byte[count];
            Buffer.BlockCopy(data, start, res, 0, count);
            return res;
        }
        public static byte[] SubArray(this byte[] data, int start) { return SubArray(data, start, data.Length - start); }

        public static IEnumerable<T> AsEnumerable<T>(this T item, Func<T, T> getNext)
            where T: class
        {
            var tmp = item;
            while (tmp != null)
            {
                yield return tmp;
                tmp = getNext(tmp);
            }
        }

        /// <summary>
        /// Enumerate an IEnumerable and perform an action on each of them. Return the enumerable.
        /// </summary>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            // WARNING do NOT use yield for that
            if (collection != null)
                foreach (var item in collection)
                    action(item);
            return collection;
        }

        public static List<T> Remove<T>(this ICollection<T> col, Predicate<T> match)
        {
            var removed = new List<T>();
            if (col is IList<T>)
            {
                var list = (IList<T>)col;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var item = list[i];
                    if (match(item))
                    {
                        list.RemoveAt(i);
                        removed.Add(item);
                    }
                }
            }
            else
            {
                var list = col.Where(x => match(x)).ToList(); // save them first, removing as we go might be problematic
                foreach (var item in list)
                {
                    col.Remove(item);
                    removed.Add(item);
                }
            }
            return removed;
        }

        public static IEnumerable<T> Remove<T>(this ICollection<T> col, params T[] items)
        {
            return col.Remove((IEnumerable<T>)items);
        }
        public static IEnumerable<T> Remove<T>(this ICollection<T> col, IEnumerable<T> items)
        {
            var removed = new List<T>();
            foreach (var item in items)
            {
                if (col.Remove(item))
                    removed.Add(item);
            }
            return removed;
        }
    }
}
