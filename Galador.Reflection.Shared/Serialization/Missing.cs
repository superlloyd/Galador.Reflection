using Galador.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
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

        public ReflectType Type { get; private set; }
        public MemberList Members { get; } = new MemberList();

        public string ConverterString { get; set; } // only when serialized with a TypeConverter ...

        public IReadOnlyList<Tuple<object, object>> Collection { get; internal set; } = Empty<Tuple<object, object>>.Array;


        #region class Member MemberList

        public class Member
        {
            public string Name { get; internal set; }
            public ReflectType Type { get; internal set; }
            public object Value { get; internal set; }
        }

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

            public Member this[string key]
            {
                get
                {
                    Member m;
                    dict.TryGetValue(key, out m);
                    return m;
                }
            }
            public Member this[int index] { get { return list[index]; } }
            public int Count { get { return list.Count; } }
            public IEnumerable<string> Keys { get { return dict.Keys; } }
            public IEnumerable<Member> Values { get { return list; } }

            public bool ContainsKey(string key) { return dict.ContainsKey(key); }
            public IEnumerator<Member> GetEnumerator() { return list.GetEnumerator(); }
            bool IReadOnlyDictionary<string, Member>.TryGetValue(string key, out Member value) { return dict.TryGetValue(key, out value); }

            IEnumerator<KeyValuePair<string, Member>> IEnumerable<KeyValuePair<string, Member>>.GetEnumerator() { return dict.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
        }

        #endregion
    }
}
