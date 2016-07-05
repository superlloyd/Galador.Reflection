using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Serialization
{
    partial class ReflectType
    {
        void SetConstructor(ConstructorInfo ctor)
        {
            // TODO use emit... (also watch out for default values)
            emtpy_constructor = ctor;
        }
        public object TryConstruct()
        {
            if (Type == null)
                return null;
            if (emtpy_constructor != null)
                return emtpy_constructor.TryConstruct();
            return Type.GetUninitializedObject();
        }
        ConstructorInfo emtpy_constructor;

        partial class Member
        {
            Type memberType;
            Action<object, object> setter;
            Func<object, object> getter;

            internal void SetMember(PropertyInfo pi)
            {
                memberType = pi.PropertyType;
                getter = EmitHelper.CreatePropertyGetterHandler(pi);
                if (pi.GetSetMethod() != null)
                    setter = EmitHelper.CreatePropertySetterHandler(pi);
            }
            internal void SetMember(FieldInfo pi)
            {
                memberType = pi.FieldType;
                getter = EmitHelper.CreateFieldGetterHandler(pi);
                setter = EmitHelper.CreateFieldSetterHandler(pi);
            }

            public object GetValue(object instance)
            {
                try
                {
                    if (getter != null)
                        return getter(instance);
                }
                catch (Exception ex)
                {
                    Logging.TraceKeys.Serialization.Error($"Error while serializing {this}, couldn't get member {Name} because {ex}");
                }
                return null;
            }
            public void SetValue(object instance, object value)
            {
                try
                {
                    if (setter != null && memberType.IsInstanceOf(value))
                        setter(instance, value);
                }
                catch (Exception ex)
                {
                    Logging.TraceKeys.Serialization.Error($"Error while deserializing {this}, couldn't set member {Name} because {ex}");
                }
            }
        }
    }
}
