using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// A class that will contain all references written or read by the <see cref="ObjectWriter"/> or <see cref="ObjectReader"/>
    /// Could be use for reference purpose.
    /// </summary>
    public class ObjectContext
    {
        internal static readonly ObjectContext WellKnownContext;

        static ObjectContext()
        {
            WellKnownContext = new ObjectContext();
            // 0 <==> null
            WellKnownContext.Register(1, ReflectType.RObject);
            WellKnownContext.Register(2, ReflectType.RString);
            WellKnownContext.Register(3, ReflectType.RType);
            WellKnownContext.Register(4, ReflectType.RReflectType);
        }

        #region serialization methods: TryGetObject() Contains() TryGetId() NewId() Register()

        Dictionary<ulong, object> idToObjects = new Dictionary<ulong, object>();
        Dictionary<object, ulong> objectsToIds = new Dictionary<object, ulong>();
        ulong seed = (ulong)(WellKnownContext != null ? WellKnownContext.Count : 0) + 1;

        /// <summary>
        /// When reading object, check whether they are already known with that method
        /// </summary>
        public bool TryGetObject(ulong id, out object o)
        {
            if (id == 0)
            {
                o = null;
                return true;
            }
            if (this != WellKnownContext && WellKnownContext.TryGetObject(id, out o))
                return true;
            return idToObjects.TryGetValue(id, out o);
        }

        /// <summary>
        /// When writing object, check if they are already registered with this method
        /// </summary>
        public bool TryGetId(object o, out ulong id)
        {
            if (o == null)
            {
                id = 0;
                return true;
            }
            if (this != WellKnownContext && WellKnownContext.TryGetId(o, out id))
                return true;
            return objectsToIds.TryGetValue(o, out id);
        }

        /// <summary>
        /// When writing object, check if they are already registered with this method
        /// </summary>
        public bool Contains(object o)
        {
            if (o == null)
                return true;
            ulong oid;
            return TryGetId(o, out oid);
        }

        /// <summary>
        /// When writing object, check if they are already registered with this method
        /// </summary>
        public bool Contains(ulong oid)
        {
            if (oid == 0)
                return true;
            object o;
            return TryGetObject(oid, out o);
        }

        /// <summary>
        /// Get a new ID to use for an object
        /// </summary>
        internal ulong NewId()
        {
            do { seed++; }
            while (Contains(seed));
            return seed;
        }

        /// <summary>
        /// Register unknown object with that method
        /// </summary>
        internal void Register(ulong id, object o)
        {
            if (this != WellKnownContext && WellKnownContext.Contains(id))
                throw new InvalidOperationException($"ID({id}) already in use");
            if (idToObjects.ContainsKey(id))
                throw new InvalidOperationException($"ID({id}) already in use");
            if (id == 0)
                throw new InvalidOperationException($"null(0) already registered");
            if (objectsToIds.ContainsKey(o))
                throw new InvalidOperationException($"Object({o}) already registered");
            idToObjects[id] = o;
            if (o != null)
                objectsToIds[o] = id;
        }

        #endregion

        #region info: Count, IDs, Objects this[]

        public int Count { get { return idToObjects.Count; } }

        public IEnumerable<ulong> IDs { get { return idToObjects.Keys; } }

        public IEnumerable<object> Objects { get { return idToObjects.Values; } }

        public object this[ulong index]
        {
            get
            {
                object o;
                if (index == 0ul)
                    return null;
                if (this != WellKnownContext && WellKnownContext.idToObjects.TryGetValue(index, out o))
                    return o;
                if (idToObjects.TryGetValue(index, out o))
                    return o;
                throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region GenerateCSharpCode()

        /// <summary>
        /// Go through all registered ReflectType and generate a C# class for it
        /// </summary>
        public void GenerateCSharpCode(TextWriter w)
        {
            // class named: $"Class{id}" with SerializableAttribute
            throw new NotImplementedException("TODO");
        }

        #endregion
    }
}
