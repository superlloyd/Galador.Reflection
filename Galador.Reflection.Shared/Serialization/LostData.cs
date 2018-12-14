using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using SRS = System.Runtime.Serialization;

namespace Galador.Reflection.Serialization
{
    public class LostData
    {
        public LostData(object target)
        {
            Target = target;
        }

        public object Target { get; }

        public SRS.SerializationInfo SerializationInfo { get; internal set; }
        public MemberList<Member> Members { get; } = new MemberList<Member>();

        public IReadOnlyList<object> IList { get; internal set; } = Array.Empty<object>();
        public IReadOnlyList<(object Key, object Value)> IDictionary { get; internal set; } = Array.Empty<(object, object)>();

        #region class Member

        public class Member : IMember
        {
            internal Member(TypeData.Member target, object value)
            {
                Name = target.Name;
                Type = target.Type;
                Value = value;
            }
            public string Name { get; }
            public TypeData Type { get; }
            public object Value { get; }
        }

        #endregion
    }
}
