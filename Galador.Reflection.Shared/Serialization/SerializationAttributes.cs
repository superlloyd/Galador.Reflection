using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// Control class serialization behavior
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class SerializationSettingsAttribute : Attribute
    {
        public SerializationSettingsAttribute()
        {
        }
        public SerializationSettingsAttribute(bool @default)
        {
            IncludePublicProperties = @default;
            IncludePrivateProperties = @default;
            IncludePublicFields = @default;
            IncludePrivateFields = @default;
        }
        public bool IncludePublicProperties { get; set; } = true;
        public bool IncludePrivateProperties { get; set; } = false;
        public bool IncludePublicFields { get; set; } = true;
        public bool IncludePrivateFields { get; set; } = false;

        public bool IncludePublics
        {
            get { return IncludePublicFields && IncludePublicProperties; }
            set
            {
                IncludePublicProperties = value;
                IncludePublicFields = value;
            }
        }
        public bool IncludePrivates
        {
            get { return IncludePrivateFields && IncludePrivateProperties; }
            set
            {
                IncludePrivateFields = value;
                IncludePrivateProperties = value;
            }
        }
        public bool IncludeProperties
        {
            get { return IncludePublicProperties && IncludePrivateProperties; }
            set
            {
                IncludePublicProperties = value;
                IncludePrivateProperties = value;
            }
        }
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public class SerializationNameAttribute : Attribute
    {
        public SerializationNameAttribute()
        {
        }
        public SerializationNameAttribute(string type, string assembly = null)
        {
            TypeName = type;
            AssemblyName = assembly;
        }

        public string TypeName { get; set; }
        public string AssemblyName { get; set; }

        public override bool Equals(object obj)
        {
            var o = obj as SerializationNameAttribute;
            if (o == null)
                return false;
            return TypeName == o.TypeName && AssemblyName == o.AssemblyName;
        }
        public override int GetHashCode()
        {
            var result = TypeName.GetHashCode();
            if (AssemblyName != null)
                result ^= AssemblyName.GetHashCode();
            return result;
        }
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
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class NotSerializedAttribute : Attribute
    {
    }
}
