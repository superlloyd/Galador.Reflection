using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public interface IMember
    {
        string Name { get; }
    }

    /// <summary>
    /// Specialized list of <see cref="FastMember"/>.
    /// </summary>
    public class MemberList<T> : IReadOnlyList<T>, IReadOnlyDictionary<string, T>
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
        /// Gets the <see cref="Member"/> with the given name.
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
        /// Gets the <see cref="Member"/> at the specified index.
        /// </summary>
        public T this[int index] { get { return list[index]; } }

        /// <summary>
        /// Number of <see cref="Member"/>.
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
