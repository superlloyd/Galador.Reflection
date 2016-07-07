using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Instance returned when the original type cannot be found.
    /// </summary>
    public sealed class Missing
    {
        internal Missing(ReflectType type)
        {
            this.Type = type;
            foreach (var m in type.RuntimeMembers())
            {
                var mm = new Member
                {
                    Name = m.Name,
                    Type = m.Type,
                };
                Members.Add(mm);
            }
        }


        /// <summary>
        /// Serialization information about this object's original type.
        /// </summary>
        public ReflectType Type { get; private set; }

        /// <summary>
        /// Serialized member values.
        /// </summary>
        public MemberList Members { get; } = new MemberList();

        /// <summary>
        /// If this object was originally serialized using a <see cref="TypeConverter"/> this is the string that was saved
        /// </summary>
        public string ConverterString { get; internal set; }

        /// <summary>
        /// Collection data that was serialized, if these type was serialized as a collection. 
        /// Also used for <see cref="System.Runtime.Serialization.ISerializable"/> type, saving the 
        /// <see cref="System.Runtime.Serialization.SerializationInfo"/> data.
        /// </summary>
        public IReadOnlyList<Tuple<object, object>> Collection { get; internal set; } = Empty<Tuple<object, object>>.Array;


        #region class Member MemberList

        /// <summary>
        /// A class that represent either property or field in a class. Identified by name.
        /// </summary>
        public class Member
        {
            /// <summary>
            /// The property or field name.
            /// </summary>
            public string Name { get; internal set; }
            /// <summary>
            /// Serialization information about this member declared type.
            /// </summary>
            public ReflectType Type { get; internal set; }
            /// <summary>
            /// Value of the member at the time it was serialized.
            /// </summary>
            public object Value { get; internal set; }
        }

        /// <summary>
        /// Specialized list of <see cref="Member"/>.
        /// </summary>
        public class MemberList : IReadOnlyList<Member>, IReadOnlyDictionary<string, Member>
        {
            List<Member> list = new List<Member>();
            Dictionary<string, Member> dict = new Dictionary<string, Member>();

            internal MemberList()
            {
            }
            internal void Add(Member m)
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
            public Member this[string name]
            {
                get
                {
                    Member m;
                    dict.TryGetValue(name, out m);
                    return m;
                }
            }
            /// <summary>
            /// Gets the <see cref="Member"/> at the specified index.
            /// </summary>
            public Member this[int index] { get { return list[index]; } }
            /// <summary>
            /// Number of <see cref="Member"/>.
            /// </summary>
            public int Count { get { return list.Count; } }
            /// <summary>
            /// All the member's names.
            /// </summary>
            public IEnumerable<string> Keys { get { return dict.Keys; } }
            /// <summary>
            /// All the members
            /// </summary>
            public IEnumerable<Member> Values { get { return list; } }

            /// <summary>
            /// Whether a member with such a name exists.
            /// </summary>
            /// <param name="name">The member's name.</param>
            /// <returns>Whether there is such a member.</returns>
            public bool ContainsKey(string name) { return dict.ContainsKey(name); }
            /// <summary>
            /// Enumerate the members.
            /// </summary>
            public IEnumerator<Member> GetEnumerator() { return list.GetEnumerator(); }
            bool IReadOnlyDictionary<string, Member>.TryGetValue(string key, out Member value) { return dict.TryGetValue(key, out value); }

            IEnumerator<KeyValuePair<string, Member>> IEnumerable<KeyValuePair<string, Member>>.GetEnumerator() { return dict.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
        }

        #endregion
    }
}
