using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Object which have a name and are used with <see cref="MemberList{T}"/>.
    /// </summary>
    public interface IMember
    {
        /// <summary>
        /// The name of this member. Use as an index in <see cref="MemberList{T}"/>.
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Specialized list of <typeparamref name="T"/> which must be <see cref="IMember"/>.
    /// Index them by both int index but also name.
    /// </summary>
    public class MemberList<T> : IReadOnlyList<T>, IReadOnlyDictionary<string, T>, IEnumerable<T>
        where T : IMember
    {
        List<T> list = new List<T>();
        Dictionary<string, T> dict = new Dictionary<string, T>();

        internal MemberList()
        {
        }

        internal void Add(T m)
        {
            if (dict.ContainsKey(m.Name))
                throw new ArgumentException();
            list.Add(m);
            dict[m.Name] = m;
        }

        /// <summary>
        /// Gets the <typeparamref name="T"/> with the given name.
        /// </summary>
        /// <param name="name">Name of the member.</param>
        /// <returns>Return the member with name, or null.</returns>
        public T this[string name]
        {
            get
            {
                T m;
                dict.TryGetValue(name, out m);
                return m;
            }
        }
        /// <summary>
        /// Gets the <typeparamref name="T"/> at the specified index.
        /// </summary>
        public T this[int index] { get { return list[index]; } }

        /// <summary>
        /// Number of members or item.
        /// </summary>
        public int Count { get { return list.Count; } }

        /// <summary>
        /// All the member's names.
        /// </summary>
        public IEnumerable<string> MemberNames { get { return dict.Keys; } }
        IEnumerable<string> IReadOnlyDictionary<string, T>.Keys { get { return dict.Keys; } }

        IEnumerable<T> IReadOnlyDictionary<string, T>.Values { get { return list; } }

        /// <summary>
        /// Whether a member with such a name exists.
        /// </summary>
        /// <param name="name">The member's name.</param>
        /// <returns>Whether there is such a member.</returns>
        public bool Contains(string name) { return dict.ContainsKey(name); }
        bool IReadOnlyDictionary<string, T>.ContainsKey(string name) { return dict.ContainsKey(name); }

        /// <summary>
        /// Enumerate the members.
        /// </summary>
        public IEnumerator<T> GetEnumerator() { return list.GetEnumerator(); }
        bool IReadOnlyDictionary<string, T>.TryGetValue(string key, out T value) { return dict.TryGetValue(key, out value); }

        IEnumerator<KeyValuePair<string, T>> IEnumerable<KeyValuePair<string, T>>.GetEnumerator() { return dict.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
    }
}
