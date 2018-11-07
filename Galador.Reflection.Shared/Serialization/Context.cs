using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class Context
    {
        static Context Wellknown()
        {
            var result = new Context();
            // 0 is null
            ulong index = 1;
            result.Register(index++, ReflectType.RObject);
            result.Register(index++, ReflectType.RString);
            result.Register(index++, ReflectType.RType);
            result.Register(index++, ReflectType.RReflectType);
            result.Register(index++, ReflectType.RNullable);
            // other well known values, to speed up read-write and reduce stream size
            result.Register(index++, "");
            result.Register(index++, ReflectType.GetType(typeof(byte[])));
            result.Register(index++, ReflectType.GetType(typeof(Guid)));
            result.Register(index++, ReflectType.GetType(typeof(bool)));
            result.Register(index++, ReflectType.GetType(typeof(char)));
            result.Register(index++, ReflectType.GetType(typeof(byte)));
            result.Register(index++, ReflectType.GetType(typeof(sbyte)));
            result.Register(index++, ReflectType.GetType(typeof(short)));
            result.Register(index++, ReflectType.GetType(typeof(ushort)));
            result.Register(index++, ReflectType.GetType(typeof(int)));
            result.Register(index++, ReflectType.GetType(typeof(uint)));
            result.Register(index++, ReflectType.GetType(typeof(long)));
            result.Register(index++, ReflectType.GetType(typeof(ulong)));
            result.Register(index++, ReflectType.GetType(typeof(float)));
            result.Register(index++, ReflectType.GetType(typeof(double)));
            result.Register(index++, ReflectType.GetType(typeof(decimal)));
            return result;
        }
        readonly static Context wellknown = Wellknown();

        readonly Dictionary<ulong, object> idToObjects = new Dictionary<ulong, object>();
        readonly Dictionary<object, ulong> objectsToIds = new Dictionary<object, ulong>();
        ulong seed = 30;

        protected void Register(ulong id, object o)
        {

        }
    }
}
