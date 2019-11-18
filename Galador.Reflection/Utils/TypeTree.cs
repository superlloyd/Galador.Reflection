using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Galador.Reflection.Utils
{
    public interface ITypeTree
    {
        int Count();
        int Count(Type root);
        IEnumerable<Type> GetKeys();
        IEnumerable<Type> GetKeys(Type root);
        void ClearValue(Type value);
        void Clear();
        bool ContainsKey(Type key);
    }

    public interface ITypeTree<TData> : ITypeTree
        where TData : class
    {
        TData this[Type key] { get; set; }
        IEnumerable<(Type key, TData value)> GetKeyValues();
        IEnumerable<(Type key, TData value)> GetKeyValues(Type root);
    }

    public class TypeTree<T> : ITypeTree<T>
        where T : class
    {
        [DebuggerDisplay("({Key.FullName}, {Data}, Descendants: {Descendants.Count})")]
        class TypeNode
        {
            public TypeNode(Type key)
            {
                Key = key;
            }
            public readonly Type Key;
            public T Data;
            public readonly List<TypeNode> Descendants = new List<TypeNode>();

            public override string ToString() => $"{nameof(TypeNode)}({Key}, {Data}, Descendants: {Descendants.Count})";

            public int Count() => (Data == null ? 0 : 1) + Descendants.Select(x => x.Count()).Sum();

            public IEnumerable<Type> SubKeys
            {
                get
                {
                    if (Data != null)
                        yield return Key;
                    foreach (var d in Descendants)
                        foreach (var sub in d.SubKeys)
                            yield return sub;
                }
            }
            public IEnumerable<(Type key, T value)> SubKeyValues
            {
                get
                {
                    if (Data != null)
                        yield return (Key, Data);
                    foreach (var d in Descendants)
                        foreach (var sub in d.SubKeyValues)
                            yield return sub;
                }
            }
        }
        Dictionary<Type, TypeNode> nodes = new Dictionary<Type, TypeNode>();

        public TypeTree()
        {
        }

        public TypeTree(Func<Type, T, bool> validation)
        {
            Validation = validation;
        }

        public Func<Type, T, bool> Validation { get; }

        public void Validate(Type type, T data)
        {
            if (Validation == null)
                return;
            if (!Validation(type, data))
                throw new ArgumentException($"Invalid Data ({data}) for Key {type}", nameof(data));
        }

        public T this[Type key]
        {
            get
            {
                if (nodes.TryGetValue(key, out var node))
                    return node.Data;
                return null;
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));
                Validate(key, value);
                GetOrCreate(key).Data = value;
            }
        }
        TypeNode GetOrCreate(Type key)
        {
            if (nodes.TryGetValue(key, out var node))
                return node;

            node = new TypeNode(key);
            nodes[key] = node;

            var pi = key.GetTypeInfo();

            if (pi.BaseType != null)
            {
                var pn = GetOrCreate(pi.BaseType);
                pn.Descendants.Add(node);
            }

            foreach (var intfc in pi.ImplementedInterfaces)
            {
                var pn = GetOrCreate(intfc);
                pn.Descendants.Add(node);
            }

            // will happen for interfaces, but make sure everything is resolved by "object"
            if (pi.BaseType == null && key != typeof(object))
            {
                var pn = GetOrCreate(typeof(object));
                pn.Descendants.Add(node);
            }

            return node;
        }

        public void Clear() => nodes.Clear();

        public void ClearValue(Type value)
        {
            if (nodes.TryGetValue(value, out var node))
                node.Data = null;
        }

        public bool ContainsKey(Type key) => nodes.TryGetValue(key, out var node) && node.Data != null;

        public int Count()
        {
            int n = 0;
            foreach (var node in nodes.Values.Where(x => x.Data != null))
                n++;
            return n;
        }

        public IEnumerable<Type> GetKeys()
        {
            foreach (var node in nodes.Values.Where(x => x.Data != null))
                yield return node.Key;
        }

        public IEnumerable<(Type key, T value)> GetKeyValues()
        {
            foreach (var node in nodes.Values.Where(x => x.Data != null))
                yield return (node.Key, node.Data);
        }

        public int Count(Type root)
        {
            if (nodes.TryGetValue(root, out var node))
                return node.Count();
            return 0;
        }

        public IEnumerable<Type> GetKeys(Type root)
        {
            if (nodes.TryGetValue(root, out var node))
                foreach (var k in node.SubKeys)
                    yield return k;
        }

        public IEnumerable<(Type key, T value)> GetKeyValues(Type root)
        {
            if (nodes.TryGetValue(root, out var node))
                foreach (var kv in node.SubKeyValues)
                    yield return kv;
        }
    }
}
