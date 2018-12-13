using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using SRS = System.Runtime.Serialization;

namespace Galador.Reflection.Serialization
{
    public class ObjectData
    {
        internal ObjectData(TypeData type)
        {
            TypeData = type;
        }

        public TypeData TypeData { get; }

        public SRS.SerializationInfo Info { get; internal set; }
        public string ConverterString { get; internal set; }
        public object SurrogateObject { get; internal set; }
        public MemberList<Member> Members { get; } = new MemberList<Member>();

        public IReadOnlyList<object> IList { get; internal set; } = Array.Empty<object>();
        public IReadOnlyList<(object Key, object Value)> IDictionary { get; internal set; } = Array.Empty<(object, object)>();

        #region class Member

        public class Member : IMember
        {
            public string Name { get; internal set; }
            public TypeData Type { get; internal set; }
            public object Value { get; internal set; }
        }

        #endregion
    }
}
