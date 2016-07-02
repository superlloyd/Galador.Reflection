using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Collection of utilities for Reflection
    /// </summary>
    public static class ReflectionEx
    {
        public static MemberInfo[] GetLambdaPath(LambdaExpression e)
        {
            object root;
            return GetLambdaPath(e, out root);
        }
        public static MemberInfo[] GetLambdaPath(LambdaExpression e, out object root)
        {
            if (e == null)
                throw new ArgumentNullException();

            root = null;
            var list = new List<MemberInfo>();
            var me = e.Body as MemberExpression;
            while (me != null)
            {
                bool ok = me.Member is PropertyInfo || me.Member is FieldInfo;
                if (!ok)
                    throw new NotSupportedException();
                list.Add(me.Member);
                switch (me.NodeType)
                {
                    case ExpressionType.Parameter:
                        //case ExpressionType.Index://.NET4 it seems...
                        throw new NotImplementedException();
                    case ExpressionType.MemberAccess:
                        if (me.Expression is ConstantExpression)
                        {
                            var ce = (ConstantExpression)me.Expression;
                            root = ce.Value;
                        }
                        me = me.Expression as MemberExpression;
                        break;
                    default:
                        me = null;
                        break;
                }
            }
            list.Reverse();
            return list.ToArray();
        }

        public static string GetName<TProperty>(this Expression<Func<TProperty>> e)
        {
            var l = GetLambdaPath(e);
            if (l.Length == 0)
                throw new ArgumentException();
            return l[l.Length - 1].Name;
        }

        public static bool IsActivable(this Type t)
        {
            return IsActivable(t.GetTypeInfo());
        }
        public static bool IsActivable(this TypeInfo ti)
        {
            if (ti.IsAbstract || ti.IsInterface)
                return false;
            var q =
                from c in ti.DeclaredConstructors
                where c.GetParameters().Length == 0
                where c.IsPublic
                select c
                ;
            return q.FirstOrDefault() != null;
        }

        public static bool IsBaseClass(this Type t, Type sub)
        {
            return t.GetTypeInfo().IsAssignableFrom(sub.GetTypeInfo());
        }
        public static bool IsInstanceOf(this Type t, object o)
        {
            if (o == null)
                return t.GetTypeInfo().IsClass || t.GetTypeInfo().IsInterface;
            return t.IsBaseClass(o.GetType());
        }

        public static bool IsFinal(this Type type)
        {
            if (type == typeof(Type))
                return true;
            var ti = type.GetTypeInfo();
            return ti.IsValueType || ti.IsSealed;
        }

        public static IEnumerable<ConstructorInfo> TryGetConstructors(this Type type, params Type[] argsType)
        {
            var ctors =
                from ci in type.GetTypeInfo().DeclaredConstructors
                let ps = ci.GetParameters()
                let N = ps.Where(x => x.DefaultValue == DBNull.Value).Count()
                where (argsType == null && N == 0) || (argsType != null && argsType.Length >= N && argsType.Length <= ps.Length)
                where Enumerable.Range(0, argsType.Length).All(i => IsBaseClass(ps[i].ParameterType, argsType[i]))
                orderby ci.IsPublic descending, ps.Length descending
                select ci;
            return ctors;
        }
        public static object TryConstruct(this Type type, params object[] args)
        {
            var ctors =
                from ci in type.GetTypeInfo().DeclaredConstructors
                let ps = ci.GetParameters()
                let N = ps.Where(x => x.DefaultValue == DBNull.Value).Count()
                where (args == null && N == 0) || (args != null && args.Length >= N && args.Length <= ps.Length)
                where Enumerable.Range(0, args.Length).All(i => IsInstanceOf(ps[i].ParameterType, args[i]))
                orderby ci.IsPublic descending, ps.Length descending
                select ci;
            var ctor = ctors.FirstOrDefault();
            if (ctor == null && type.GetTypeInfo().IsValueType && (args == null || args.Length == 0))
                return Activator.CreateInstance(type);
            if (ctor == null)
                return null;
            return TryConstruct(ctor, args);
        }
        public static object TryConstruct(this ConstructorInfo ctor, params object[] args)
        {
            var ps = ctor.GetParameters();
            var cargs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (args == null || args.Length <= i)
                {
                    if (p.DefaultValue == DBNull.Value)
                        return null;
                    cargs[i] = p.DefaultValue;
                }
                else
                {
                    if (!IsInstanceOf(p.ParameterType, args[i]))
                        return null;
                    cargs[i] = args[i];
                }
            }
            return ctor.Invoke(cargs);
        }

        public static IEnumerable<MethodInfo> TryGetMethods(this Type type, string method, Type[] genericArgs, params Type[] argsType)
        {
            var methods =
                from m in type.GetTypeInfo().GetRuntimeMethods()
                where !m.IsSpecialName
                where m.Name == method
                where (m.IsGenericMethod && genericArgs != null && genericArgs.Length == m.GetGenericArguments().Length) 
                    || (!m.IsGenericMethod && (genericArgs == null || genericArgs.Length == 0))
                let mi = !m.IsGenericMethod ? m : m.MakeGenericMethod(genericArgs)
                let p = mi.GetParameters()
                let N = p.Where(x => x.DefaultValue == DBNull.Value).Count()
                where (argsType == null && N == 0) || (argsType != null && argsType.Length >= N && argsType.Length <= p.Length)
                where Enumerable.Range(0, argsType.Length).All(i => IsBaseClass(p[i].ParameterType, argsType[i]))
                orderby mi.IsPublic descending, p.Length - N
                select mi;
            return methods;
        }

        public static IEnumerable<Type> GetTypeHierarchy(this Type t) { return GetAllTypes(t).Distinct(); }
        static IEnumerable<Type> GetAllTypes(Type t)
        {
            var p = t;
            while (p != null)
            {
                yield return p;
                foreach (var item in p.GetTypeInfo().ImplementedInterfaces)
                    foreach (var sub in GetAllTypes(item))
                        yield return sub;
                p = p.GetTypeInfo().BaseType;
            }
        }

#if __PCL__
        public static Type GetEnumUnderlyingType(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        public static IEnumerable<PropertyInfo> GetRuntimeProperties(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        public static IEnumerable<MethodInfo> GetRuntimeMethods(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        public static IEnumerable<FieldInfo> GetRuntimeFields(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
#endif
        public static T GetUninitializedObject<T>() { return (T)GetUninitializedObject(typeof(T)); }
        public static object GetUninitializedObject(this Type type)
        {
#if __PCL__
            throw new PlatformNotSupportedException(); 
#else
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif
        }

    }
}
