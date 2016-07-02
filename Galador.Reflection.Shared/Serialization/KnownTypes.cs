using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Galador.Reflection.Utils;
using Galador.Reflection.Logging;
using System.Runtime.InteropServices;

namespace Galador.Reflection.Serialization
{
    public static class KnownTypes
    {
        static KnownTypes()
        {
            Register(typeof(KnownTypes).GetTypeInfo().Assembly);
        }

        #region ToString() Parse() GetType()

        public static string ToString(Type type) { return new TypeDescription(type).ToString(); }

        public static Type Parse(string s) { return new TypeDescription(s).Resolve(); }

        public static Type GetType(object o)
        {
            if (o == null)
                return typeof(object);
            if (o is Type)
                return typeof(Type);
            return o.GetType();
        }

        public static Type GetType(string typeName, string assemblyName)
        {
            Type result;
            var sn = new SerializationNameAttribute(typeName, assemblyName);
            lock (sReplacementTypes)
                if (sReplacementTypes.TryGetValue(sn, out result))
                    return result;

            if (assemblyName == null)
                return Type.GetType(typeName);

            var t = Type.GetType($"{typeName},{assemblyName}");
            if (t != null)
                return t;

#if !__PCL__
            var tass = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName);
            if (tass != null)
                return tass.GetType(typeName);
#endif
            return null;
        }
        public static Type GetType(PrimitiveType type)
        {
            switch (type)
            {
                default:
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    return null;
                case PrimitiveType.String: return typeof(string);
                case PrimitiveType.Bytes: return typeof(byte[]);
                case PrimitiveType.Guid: return typeof(Guid);
                case PrimitiveType.Bool: return typeof(bool);
                case PrimitiveType.Char: return typeof(char);
                case PrimitiveType.Byte: return typeof(byte);
                case PrimitiveType.SByte: return typeof(sbyte);
                case PrimitiveType.Int16: return typeof(short);
                case PrimitiveType.UInt16: return typeof(ushort);
                case PrimitiveType.Int32: return typeof(int);
                case PrimitiveType.UInt32: return typeof(uint);
                case PrimitiveType.Int64: return typeof(long);
                case PrimitiveType.UInt64: return typeof(ulong);
                case PrimitiveType.Single: return typeof(float);
                case PrimitiveType.Double: return typeof(double);
                case PrimitiveType.Decimal: return typeof(decimal);
            }
        }

        #endregion

        #region GetKind()

        public static PrimitiveType GetKind(object o)
        {
            if (o == null)
                return PrimitiveType.Object;
            return GetKind(GetType(o));
        }

        public static PrimitiveType GetKind(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type == typeof(string)) return PrimitiveType.String;
            if (type == typeof(byte[])) return PrimitiveType.Bytes;
            if (type == typeof(Guid)) return PrimitiveType.Guid;
            if (type == typeof(bool)) return PrimitiveType.Bool;
            if (type == typeof(char)) return PrimitiveType.Char;
            if (type == typeof(byte)) return PrimitiveType.Byte;
            if (type == typeof(sbyte)) return PrimitiveType.SByte;
            if (type == typeof(short)) return PrimitiveType.Int16;
            if (type == typeof(ushort)) return PrimitiveType.UInt16;
            if (type == typeof(int)) return PrimitiveType.Int32;
            if (type == typeof(uint)) return PrimitiveType.UInt32;
            if (type == typeof(long)) return PrimitiveType.Int64;
            if (type == typeof(ulong)) return PrimitiveType.UInt64;
            if (type == typeof(float)) return PrimitiveType.Single;
            if (type == typeof(double)) return PrimitiveType.Double;
            if (type == typeof(decimal)) return PrimitiveType.Decimal;
            return PrimitiveType.Object;
        }

        #endregion

        static Dictionary<Type, Type> typeToSurrogate = new Dictionary<Type, Type>();
        static Dictionary<SerializationNameAttribute, Type> sReplacementTypes = new Dictionary<SerializationNameAttribute, Type>();

        #region Register() TryGetSurrogateType()

        public static void Register(params Assembly[] ass) { Register((IEnumerable<Assembly>)ass); }
        public static void Register(IEnumerable<Assembly> assemblies)
        {
            var q =
                from ass in assemblies
                from t in ass.DefinedTypes
                select t;
            q.ForEach(t => Register(t.AsType()));
        }
        public static void Register(Type type)
        {
            lock (typeToSurrogate)
            {
                foreach (var t in GetSurrogateElements(type))
                {
                    typeToSurrogate[t] = type;
                }
            }
            var nattr = type.GetTypeInfo().GetCustomAttribute<SerializationNameAttribute>();
            if (nattr != null)
                lock (typeToSurrogate)
                    sReplacementTypes[nattr] = type;
        }

        public static bool TryGetSurrogateType(Type type, out Type result)
        {
            result = null;
            lock (typeToSurrogate)
                if (typeToSurrogate.TryGetValue(type, out result))
                    return true;

            if (type.GetTypeInfo().IsGenericType)
            {
                var t2 = type.GetGenericTypeDefinition();
                Type result0;
                lock (typeToSurrogate)
                    typeToSurrogate.TryGetValue(t2, out result0);
                if (result0 != null)
                {
                    result = result0.MakeGenericType(type.GenericTypeArguments);
                    return true;
                }
            }
            return false;
        }

        static IEnumerable<Type> GetSurrogateElements(Type type)
        {
            if (type.GetTypeInfo().IsAbstract)
                yield break;
            if (!type.TryGetConstructors().Any())
                yield break;
            foreach (var t in type.GetTypeHierarchy())
            {
                if (!t.GetTypeInfo().IsGenericType)
                    continue;
                var t2 = t.GetGenericTypeDefinition();
                if (t2 != typeof(ISurrogate<>))
                    continue;
                var t3 = t.GetTypeInfo().GenericTypeArguments[0];
                if (t3.GetTypeInfo().IsGenericType)
                {
                    if (!type.GetTypeInfo().IsGenericType)
                        continue;
                    if (t3.GenericTypeArguments.Length != type.GetTypeInfo().GenericTypeParameters.Length)
                        continue;
                    if (Enumerable.Range(0, type.GetTypeInfo().GenericTypeParameters.Length)
                        .Any(i => t3.GenericTypeArguments[i] != type.GetTypeInfo().GenericTypeParameters[i]))
                        continue;
                    var t4 = t3.GetGenericTypeDefinition();
                    t3 = t4;
                }
                yield return t3;
            }
        }

        #endregion
    }
}
