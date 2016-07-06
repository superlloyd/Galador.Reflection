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
        /// <summary>
        /// Gets the member path from a lambda expression
        /// </summary>
        /// <param name="e">The expression to parse.</param>
        /// <returns>All the members that are called in order.</returns>
        /// <exception cref="System.ArgumentNullException">If the expression is null.</exception>
        /// <exception cref="System.NotSupportedException">If the lambda expression is not a property of field path.</exception>
        /// <exception cref="System.NotImplementedException">if the lambad contains an index / array expression</exception>
        public static MemberInfo[] GetLambdaPath(LambdaExpression e)
        {
            object root;
            return GetLambdaPath(e, out root);
        }
        /// <summary>
        /// Gets the member path from a lambda expression
        /// </summary>
        /// <param name="e">The expression to parse.</param>
        /// <param name="root">The root of the expression.</param>
        /// <returns>All the members that are called in order.</returns>
        /// <exception cref="System.ArgumentNullException">If the expression is null.</exception>
        /// <exception cref="System.NotSupportedException">If the lambda expression is not a property of field path.</exception>
        /// <exception cref="System.NotImplementedException">if the lambad contains an index / array expression</exception>
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

        /// <summary>
        /// Gets the name of the last member expression of this lambda expression.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="e">The expression to parse.</param>
        /// <returns>A member (property or field) name.</returns>
        /// <exception cref="System.ArgumentException">If the expression is empty</exception>
        public static string GetName<TProperty>(this Expression<Func<TProperty>> e)
        {
            var l = GetLambdaPath(e);
            if (l.Length == 0)
                throw new ArgumentException();
            return l[l.Length - 1].Name;
        }

        /// <summary>
        /// Determines whether this type has a parameterless constructor
        /// </summary>
        /// <param name="t">The type to assess.</param>
        /// <returns>Whether the type has a parameterless constructor.</returns>
        public static bool IsActivable(this Type t)
        {
            return IsActivable(t.GetTypeInfo());
        }
        /// <summary>
        /// Determines whether this type has a parameterless constructor
        /// </summary>
        /// <param name="ti">The type to assess.</param>
        /// <returns>Whether the type has a parameterless constructor.</returns>
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

        /// <summary>
        /// Determines whether <paramref name="t"/> is a base class of <paramref name="sub"/>
        /// </summary>
        /// <param name="t">The potential base class.</param>
        /// <param name="sub">The potential subclass.</param>
        /// <returns>Whether <paramref name="sub"/> is a <paramref name="t"/></returns>
        public static bool IsBaseClass(this Type t, Type sub)
        {
            return t.GetTypeInfo().IsAssignableFrom(sub.GetTypeInfo());
        }
        /// <summary>
        /// Determines whether <paramref name="t"/> is a base class of <paramref name="o"/>
        /// </summary>
        /// <param name="t">The potential base class.</param>
        /// <param name="o">An instance of a possible subclass.</param>
        /// <returns>Whether <paramref name="o"/> is a <paramref name="t"/></returns>
        public static bool IsInstanceOf(this Type t, object o)
        {
            if (o == null)
                return t.GetTypeInfo().IsClass || t.GetTypeInfo().IsInterface;
            return t.IsBaseClass(o.GetType());
        }

        /// <summary>
        /// Determines whether <paramref name="type"/> is final, i.e. is a value type or a sealed class.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>Whether the type can NOT have subclass or not.</returns>
        public static bool IsFinal(this Type type)
        {
            if (type == typeof(Type))
                return true;
            var ti = type.GetTypeInfo();
            return ti.IsValueType || ti.IsSealed || ti.IsArray;
        }

        /// <summary>
        /// Try the get constructors that match the given arguments types. This method will consider public constructor first and will also consider default values.
        /// </summary>
        /// <param name="type">The type where constructors will be searched.</param>
        /// <param name="argsType">Type of the arguments.</param>
        /// <returns>A list of constructors probably with 0 or 1 element, or more in case of overloading.</returns>
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

        /// <summary>
        /// Try to find a constructor matching the arguments and call it. It will also consider default parameters value.
        /// </summary>
        /// <param name="type">The type to search for constructor.</param>
        /// <param name="args">Constructor arguments.</param>
        /// <returns>A new object in case of success or null.</returns>
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

        /// <summary>
        /// Try to call a constructor with the given argument. Will check argument type and will fill the blank with default parameter values.
        /// </summary>
        /// <param name="ctor">The constructor to used.</param>
        /// <param name="args">The arguments to use.</param>
        /// <returns>A newly constructed object, or null.</returns>
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

        /// <summary>
        /// Try the get a method by name, generic type argument and normal argument type.
        /// </summary>
        /// <param name="type">The type that is searched.</param>
        /// <param name="method">The method.</param>
        /// <param name="genericArgs">The generic arguments.</param>
        /// <param name="argsType">Type of the arguments.</param>
        /// <returns>A (possibly empty) list of method that match all criteria.</returns>
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

        /// <summary>
        /// Get all base type and interfaces implemented by this type.
        /// </summary>
        /// <param name="t">The type to expand.</param>
        /// <returns>A list of types which can be used a base class of arg type.</returns>
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
#pragma warning disable 1591 // XML Comments

        internal static Type GetEnumUnderlyingType(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        internal static IEnumerable<PropertyInfo> GetRuntimeProperties(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        internal static IEnumerable<MethodInfo> GetRuntimeMethods(this TypeInfo ti) { throw new PlatformNotSupportedException(); }
        internal static IEnumerable<FieldInfo> GetRuntimeFields(this TypeInfo ti) { throw new PlatformNotSupportedException(); }

#pragma warning restore 1591 // XML Comments
#endif
        /// <summary>
        /// Return an uninitialized object (by passing constructor).
        /// </summary>
        /// <typeparam name="T">The type to construct.</typeparam>
        /// <returns>An uninitialized object</returns>
        public static T GetUninitializedObject<T>() { return (T)GetUninitializedObject(typeof(T)); }

        /// <summary>
        /// Return an uninitialized object (by passing constructor).
        /// </summary>
        /// <param name="type">The type to construct.</param>
        /// <returns>An uninitialized object</returns>
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
