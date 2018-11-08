using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public class Context
    {
        // wellknown serialized objects
        static Context Wellknown()
        {
            var result = new Context();
            // 0 is null
            ulong index = 1;
            result.Register(index++, RuntimeType.GetType(typeof(object)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(string)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Type)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Nullable<>)).TypeData());
            // other well known values, to speed up read-write and reduce stream size
            result.Register(index++, "");
            result.Register(index++, RuntimeType.GetType(typeof(byte[])).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(Guid)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(bool)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(char)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(byte)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(sbyte)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(short)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(ushort)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(int)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(uint)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(long)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(ulong)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(float)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(double)).TypeData());
            result.Register(index++, RuntimeType.GetType(typeof(decimal)).TypeData());
            return result;
        }
        readonly static Context wellknown = Wellknown();

        // wellknown Runtime objects
        public static readonly RuntimeType RObject = RuntimeType.GetType(typeof(object));
        public static readonly RuntimeType RString = RuntimeType.GetType(typeof(string));
        public static readonly RuntimeType RType = RuntimeType.GetType(typeof(Type));

        // context code
        readonly Dictionary<ulong, object> idToObjects = new Dictionary<ulong, object>();
        readonly Dictionary<object, ulong> objectsToIds = new Dictionary<object, ulong>();
        ulong seed = 30;

        protected void Register(ulong id, object o)
        {
            if (Contains(id))
                throw new InvalidOperationException($"{id} already registered");
            if (Contains(o))
                throw new InvalidOperationException($"{o} already registered");

            idToObjects[id] = o;
            objectsToIds[o] = id;
        }

        protected ulong NewId() => seed++;

        protected virtual object ToInternals(object obj)
        {
            if (obj is Type)
                return RuntimeType.GetType((Type)obj).TypeData();
            return obj;
        }
        protected virtual object FromInternals(object obj)
        {
            if (obj is TypeData)
                return ((TypeData)obj).Target()?.Type;
            return obj;
        }

        public bool Contains(ulong ID)
        {
            if (ID == 0)
                return true;
            if (wellknown != null && wellknown.idToObjects.ContainsKey(ID))
                return true;
            return idToObjects.ContainsKey(ID);
        }

        public bool Contains(object o)
        {
            o = ToInternals(o);
            if (o == null)
                return true;
            if (wellknown != null && wellknown.objectsToIds.ContainsKey(o))
                return true;
            return objectsToIds.ContainsKey(o);
        }

        public bool TryGetObject(ulong id, out object o)
        {
            if (id == 0)
            {
                o = null;
                return true;
            }

            if (wellknown != null && wellknown.TryGetObject(id, out o))
                return true;
            return idToObjects.TryGetValue(id, out o);
        }

        public bool TryGetId(object o, out ulong id)
        {
            o = ToInternals(o);
            if (o == null)
            {
                id = 0;
                return true;
            }

            if (wellknown != null && wellknown.TryGetId(o, out id))
                return true;
            return objectsToIds.TryGetValue(o, out id);
        }

        public int Count { get { return idToObjects.Count; } }

        public IEnumerable<object> Objects { get { return idToObjects.Values; } }

    }
}
