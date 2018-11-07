using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Attribute that control serialization behavior for a particular type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class SerializationSettingsAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationSettingsAttribute"/> class.
        /// </summary>
        public SerializationSettingsAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationSettingsAttribute"/> class.
        /// </summary>
        /// <param name="default">Set all properties to <paramref name="default"/>.</param>
        public SerializationSettingsAttribute(bool @default)
        {
            IncludePublicProperties = @default;
            IncludePrivateProperties = @default;
            IncludePublicFields = @default;
            IncludePrivateFields = @default;
            IncludeListInterface = @default;
            IncludeDictionaryInterface = @default;
        }

        /// <summary>
        /// Whether public properties of this type should be serialized or not. Default value is <c>true</c>.
        /// </summary>
        public bool IncludePublicProperties { get; set; } = true;

        /// <summary>
        /// Whether private properties of this type should be serialized or not. Default value is <c>false</c>.
        /// </summary>
        public bool IncludePrivateProperties { get; set; } = false;

        /// <summary>
        /// Whether public fields of this type should be serialized or not. Default value is <c>true</c>.
        /// </summary>
        public bool IncludePublicFields { get; set; } = true;

        /// <summary>
        /// Whether private fields of this type should be serialized or not. Default value is <c>false</c>.
        /// </summary>
        public bool IncludePrivateFields { get; set; } = false;

        /// <summary>
        /// Include items from <see cref="System.Collections.IList"/> and <see cref="IList{T}"/> interface
        /// </summary>
        public bool IncludeListInterface { get; set; } = true;

        /// <summary>
        /// Include items from <see cref="System.Collections.IDictionary"/> and <see cref="IDictionary{TKey, TValue}"/> interface
        /// </summary>
        public bool IncludeDictionaryInterface { get; set; } = true;

        /// <summary>
        /// Will get or set whether public properties AND public fields of this type must be serialized.
        /// </summary>
        public bool IncludePublics
        {
            get { return IncludePublicFields && IncludePublicProperties; }
            set
            {
                IncludePublicProperties = value;
                IncludePublicFields = value;
            }
        }

        /// <summary>
        /// Will get or set whether private properties AND private fields of this type must be serialized.
        /// </summary>
        public bool IncludePrivates
        {
            get { return IncludePrivateFields && IncludePrivateProperties; }
            set
            {
                IncludePrivateFields = value;
                IncludePrivateProperties = value;
            }
        }

        /// <summary>
        /// Will get or set whether all properties of this type (whether public or private) must be serialized.
        /// </summary>
        public bool IncludeProperties
        {
            get { return IncludePublicProperties && IncludePrivateProperties; }
            set
            {
                IncludePublicProperties = value;
                IncludePrivateProperties = value;
            }
        }

        /// <summary>
        /// Will get or set whether all fields of this type (whether public or private) must be serialized.
        /// </summary>
        public bool IncludeFields
        {
            get { return IncludePrivateFields && IncludePublicFields; }
            set
            {
                IncludePrivateFields = value;
                IncludePublicFields = value;
            }
        }
    }

    /// <summary>
    /// This attribute will override default type name and assembly name used to identify the type they decorate.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public class SerializationNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationNameAttribute"/> class.
        /// </summary>
        public SerializationNameAttribute()
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationNameAttribute"/> class.
        /// </summary>
        /// <param name="type">The <see cref="TypeName"/> to use.</param>
        /// <param name="assembly">The <see cref="AssemblyName"/> to use.</param>
        public SerializationNameAttribute(string type, string assembly = null)
        {
            if (type == null)
                throw new ArgumentNullException($"parameter {type} should not be null.");
            TypeName = type;
            AssemblyName = assembly;
        }

        /// <summary>
        /// Value to use in lieu of the <see cref="Type.FullName"/>
        /// </summary>
        public string TypeName { get; set; }
        /// <summary>
        /// Value to use in lieu of the <see cref="System.Reflection.AssemblyName.Name"/>.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Determines <paramref name="obj"/> is another <see cref="SerializationNameAttribute"/> with the same property values.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var o = obj as SerializationNameAttribute;
            if (o == null)
                return false;
            return TypeName == o.TypeName && AssemblyName == o.AssemblyName;
        }
        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            var result = TypeName.GetHashCode();
            if (AssemblyName != null)
                result ^= AssemblyName.GetHashCode();
            return result;
        }

        public override string ToString() => $"SerializationName({TypeName}, {AssemblyName})";
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public class SerializationGuidAttribute : SerializationNameAttribute
    {
        public SerializationGuidAttribute(string guid)
            : base(guid)
        {
            Guid.Parse(guid);
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SerializationMemberNameAttribute : Attribute
    {
        public SerializationMemberNameAttribute()
        {
        }
        public SerializationMemberNameAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException($"parameter {name} should not be null.");
            MemberName = name;
        }

        public string MemberName { get; set; }

        /// <summary>
        /// Determines <paramref name="obj"/> is another <see cref="SerializationNameAttribute"/> with the same property values.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var o = obj as SerializationMemberNameAttribute;
            if (o == null)
                return false;
            return MemberName == o.MemberName;
        }
        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode() => MemberName?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Property or Field with this attribute will be serialized regardless of the <see cref="SerializationSettingsAttribute"/> of their class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SerializedAttribute : Attribute
    {
    }

    /// <summary>
    /// Property or Field with this attribute will NOT be serialized regardless of the <see cref="SerializationSettingsAttribute"/> of their class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class NotSerializedAttribute : Attribute
    {
    }
}
