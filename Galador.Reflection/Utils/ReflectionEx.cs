using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Collection of utilities for Reflection
    /// </summary>
    public static class ReflectionEx
    {
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
            catch
            {
                Log.Warning(typeof(KnownAssemblies).FullName, $"Couldn't get Types from {assembly.GetName().Name})");
                return Array.Empty<Type>();
            }
        }

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
        /// When a property is overriden in a class, return the original property
        /// </summary>
        public static PropertyInfo GetBaseBroperty(this PropertyInfo pi)
        {
            if (pi == null)
                return null;

            while (!pi.IsBaseProperty())
            {
                var flags = 
                    BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    ;

                var aPI = pi.DeclaringType.BaseType.GetProperty(pi.Name, flags);
                if (aPI == null)
                    return pi;
                pi = aPI;
            }
            return pi;
        }

        /// <summary>
        /// When a property is overriden in a class, whether this is the original property, or not
        /// </summary>
        public static bool IsBaseProperty(this PropertyInfo pi)
        {
            if (pi.GetMethod != null && pi.GetMethod.GetBaseDefinition() == pi.GetMethod)
                return true;
            if (pi.SetMethod != null && pi.SetMethod.GetBaseDefinition() == pi.SetMethod)
                return true;
            return false;
        }

        /// <summary>
        /// Determines whether <paramref name="t"/> is a base class of <paramref name="sub"/>
        /// </summary>
        /// <param name="t">The potential base class.</param>
        /// <param name="sub">The potential subclass.</param>
        /// <returns>Whether <paramref name="sub"/> is a <paramref name="t"/></returns>
        public static bool IsBaseClass(this Type t, Type sub)
        {
            return t.IsAssignableFrom(sub);
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
                return t.IsClass || t.IsInterface;
            return t.IsBaseClass(o.GetType());
        }

        static ParameterInfo[] SafeGetParameters(Type type, ConstructorInfo ci)
        {
            try
            {
                var parameters = ci.GetParameters();
                if (parameters == null || parameters.Length == 0)
                    return [];

                foreach (var p in parameters)
                {
                    // check that one can read the default value, it cause issues in .NET9
                    var has = p.HasDefaultValue;
                }
                return parameters;
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Debug($"Couldn't Load Constructor {type.Name}({ci}): {ex.Message}");
                }
                catch
                {
                    Log.Debug($"Couldn't Load a Constructor for {type.Name}: {ex.Message}");
                }
            }
            return null;
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
                from ci in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                where !ci.IsStatic
                let ps = SafeGetParameters(type, ci)
                where ps != null
                let N = ps.Where(x => !x.HasDefaultValue).Count()
                where (argsType == null && N == 0) || (argsType != null && argsType.Length >= N && argsType.Length <= ps.Length)
                where argsType.Length == 0 || Enumerable.Range(0, argsType.Length).All(i => IsBaseClass(ps[i].ParameterType, argsType[i]))
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
        public static T TryConstruct<T>(params object[] args)
            => (T)TryConstruct(typeof(T), args);

        /// <summary>
        /// Try to find a constructor matching the arguments and call it. It will also consider default parameters value.
        /// </summary>
        /// <param name="type">The type to search for constructor.</param>
        /// <param name="args">Constructor arguments.</param>
        /// <returns>A new object in case of success or null.</returns>
        public static object TryConstruct(this Type type, params object[] args)
        {
            var ctors =
                from ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                let cparams = ctor.GetParameters()
                where cparams.All(pi => GetParameters(pi).Any())
                let pc = cparams.Select(pi => GetParameters(pi).First()).Count(x => x != null)
                orderby pc descending, cparams.Length ascending, ctor.IsPublic descending
                select new
                {
                    Ctor = ctor,
                    Args = cparams.Select(pi => GetParameters(pi).First()).ToArray(),
                };

            var info = ctors.FirstOrDefault();
            if (info == null)
            {
                if (type.IsValueType)
                    return Activator.CreateInstance(type);
                return null;
            }
            return info.Ctor.Invoke(type, info.Args);

            IEnumerable<object> GetParameters(ParameterInfo pi)
            {
                foreach (var x in args.Where(x => x != null).Where(x => pi.ParameterType.IsInstanceOf(x)))
                    yield return x;

                if (pi.HasDefaultValue)
                    yield return pi.DefaultValue;

                if (pi.ParameterType.IsClass || pi.ParameterType.IsInterface)
                    yield return null;
            }
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
                let N = p.Where(x => !x.HasDefaultValue).Count()
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

        internal static IEnumerable<MethodInfo> GetRuntimeMethods(this TypeInfo ti)
        {
            var aTi = ti;
            while (aTi != null)
            {
                foreach (var m in ti.DeclaredMethods)
                    yield return m;
                aTi = aTi.BaseType?.GetTypeInfo();
            }
        }

        /// <summary>
        /// Return an uninitialized object (by passing constructor).
        /// </summary>
        /// <param name="type">The type to construct.</param>
        /// <returns>An uninitialized object</returns>
        internal static object GetUninitializedObject(this Type type)
        {
#if NETFRAMEWORK
            return FormatterServices.GetSafeUninitializedObject(type);
#else
            return RuntimeHelpers.GetUninitializedObject(type);
#endif
        }

    }
}
