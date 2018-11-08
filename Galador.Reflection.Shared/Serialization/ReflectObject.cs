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
    public sealed class ReflectObject
    {
        internal ReflectObject(ReflectType2 type)
        {
            this.Type = type;
            foreach (var m in type.RuntimeMembers)
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
        public ReflectType2 Type { get; private set; }

        /// <summary>
        /// Serialized member values.
        /// </summary>
        public MemberList<Member> Members { get; } = new MemberList<Member>();

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


        #region class Member

        /// <summary>
        /// A class that represent either property or field in a class. Identified by name.
        /// </summary>
        public class Member : IMember
        {
            /// <summary>
            /// The property or field name.
            /// </summary>
            public string Name { get; internal set; }
            /// <summary>
            /// Serialization information about this member declared type.
            /// </summary>
            public ReflectType2 Type { get; internal set; }
            /// <summary>
            /// Value of the member at the time it was serialized.
            /// </summary>
            public object Value { get; internal set; }
        }

        #endregion
    }
}
