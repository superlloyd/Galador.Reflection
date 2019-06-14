using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Galador.Reflection.Utils
{
    public class ServiceDefinition
    {
        public ServiceDefinition(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            Type = type;
        }
        public ServiceDefinition(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value is Type type) Type = type;
            else Instance = value;
        }
        public Type Type { get; }
        public object Instance { get; }

        public override string ToString() => $"{nameof(ServiceDefinition)}({Type}, {Instance})";
    }

    public interface ICreateCallback
    {
        /// <summary>
        /// Called when the full object tree (i.e. with all dependency) has been created.
        /// </summary>
        void OnCreated();
    }

    public static class TypeTreeActivation
    {
        public static bool IsBaseClass(Type parent, Type child)
        {
            return parent.GetTypeInfo().IsAssignableFrom(child.GetTypeInfo());
        }
        public static bool IsInstance(Type parent, object child)
        {
            if (child == null)
                return parent.GetTypeInfo().IsClass;
            return IsBaseClass(parent, child.GetType());
        }
        public static bool CanBeInstantiated(Type type)
        {
            var t = type.GetTypeInfo();
            if (t.IsAbstract || t.IsInterface)
                return false;
            if (t.IsValueType)
                return true;
            return t.DeclaredConstructors.Any();
        }

        public static void Register(ITypeTree<ServiceDefinition> services, Type facade, object instance)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (facade == null)
                throw new ArgumentNullException(nameof(facade));
            services[facade] = new ServiceDefinition(instance ?? facade);
        }

        static IEnumerable<Type> MergeKeys(ITypeTree first, ITypeTree second, Type type)
            => (first?.GetKeys(type) ?? Array.Empty<Type>())
            .Concat(second?.GetKeys(type) ?? Array.Empty<Type>())
            .Distinct();

        static IEnumerable<Type> MergeKeys(ITypeTree first, ITypeTree second, Predicate<Type> matching)
            => (first?.GetKeys() ?? Array.Empty<Type>())
            .Concat(second?.GetKeys() ?? Array.Empty<Type>())
            .Where(x => matching(x))
            .Distinct();

        public static void ResolveProperties(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (scope == null)
                scope = new TypeTree<object>();

            var ftype = FastType.GetType(instance.GetType());
            var props = (
                from p in ftype.GetRuntimeMembers()
                where !p.IsStatic
                let pi = p.Member.GetCustomAttributes<ImportAttribute>().FirstOrDefault()
                where pi != null
                select new { p, pi }
            ).ToList();
            foreach (var item in props)
            {
                var p = item.p;
                var pi = item.pi;
                if (IsBaseClass(typeof(IEnumerable), p.Type.Type))
                {
                    var t = pi.ImportedType;
                    // T[]
                    if (p.Type.Type.IsArray)
                    {
                        if (t == null)
                            t = p.Type.Type.GetElementType();
                        if (!IsBaseClass(p.Type.Type.GetElementType(), t))
                            throw new NotSupportedException($"Property {instance.GetType().Name}.{p.Name}, can't import {t.Name}");
                        var values = MergeKeys(services, scope, t).Select(x => SolveExact(services, scope, x)).ToList();
                        var prop = Array.CreateInstance(t, values.Count);
                        for (int i = 0; i < values.Count; i++)
                            prop.SetValue(values[i], i);
                        item.p.SetValue(instance, prop);
                    }
                    // List<T>
                    else
                    {
                        if (!IsBaseClass(typeof(IList), p.Type.Type))
                            throw new InvalidOperationException($"[Import] property {instance.GetType().Name}.{p.Name} must be an array or an IList");

                        if (t == null)
                        {
                            var ga = p.Type.Type.GenericTypeArguments;
                            if (ga.Length != 1)
                                throw new NotSupportedException($"[Import] property {instance.GetType().Name}.{p.Name} must be generic or the Import type must be defined");
                            t = ga[0];
                        }
                        var value = p.GetValue(instance);
                        if (value == null)
                        {
                            if (!CanBeInstantiated(p.Type.Type) || !p.CanSet)
                                throw new InvalidOperationException($"Can't [Import]{p.Type.Type.Name} for {instance.GetType().Name}.{p.Name}");
                            value = Create(services, scope, p.Type.Type);
                            p.SetValue(instance, value);
                        }
                        var list = (IList)value;
                        var values = MergeKeys(services, scope, t).Select(x => SolveExact(services, scope, x)).ToList();
                        foreach (var x in values)
                            list.Add(x);
                    }
                }
                else
                {
                    // simple property
                    var o = Resolve(services, scope, pi.ImportedType ?? p.Type.Type).First();
                    item.p.SetValue(instance, o);
                }
            }
        }

        public static IEnumerable<object> Resolve(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
            => Resolve(services, scope, type, MergeKeys(services, scope, type));

        public static IEnumerable<object> Resolve(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Predicate<Type> matching)
            => Resolve(services, scope, null, MergeKeys(services, scope, matching));

        static IEnumerable<object> Resolve(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type root, IEnumerable<Type> types)
        {
            if (scope == null)
                scope = new TypeTree<object>();

            var any = false;
            foreach (var key in types)
            {
                any = true;
                yield return SolveExact(services, scope, key);
            }

            if (!any && root != null && CanCreate(services, scope, root))
            {
                var result = Create(services, scope, root);
                scope[root] = result;
                yield return result;
            }
        }

        static object SolveExact(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
        {
            if (services?.ContainsKey(type) ?? false)
            {
                var sd = services[type];
                if (sd.Instance != null)
                {
                    return sd.Instance;
                }

                if (!scope.ContainsKey(type))
                {
                    var value = Create(services, scope, type);
                    scope[type] = value;
                    return value;
                }
            }
            return scope[type];
        }

        public static object Create(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type, params object[] parameters)
        {
            if (scope == null)
                scope = new TypeTree<object>();

            bool first = createSession == null;
            if (first)
                createSession = new List<ICreateCallback>();
            try
            {
                var ctors = FindConstructors(services, scope, type);
                var qc = (
                    from c in ctors
                    where c.IsPublic // private constructor are private for a reason, don't use them!
                    let ppp = c.GetParameters()
                    orderby c.IsPublic descending, ppp.Length descending
                    select new { c, ppp }
                )
                .FirstOrDefault();
                if (qc == null)
                    throw new InvalidOperationException($"Type {type.FullName} can't be Activated, no constructor can be called at this time.");

                var cargs = new object[qc.ppp.Length];
                for (int i = 0; i < qc.ppp.Length; i++)
                {
                    var p = qc.ppp[i];
                    var impa = p.GetCustomAttribute<ImportAttribute>();
                    var t = (impa != null ? impa.ImportedType : null) ?? p.ParameterType;
                    var vault = MergeKeys(services, scope, t).Where(x => x != t).FirstOrDefault();
                    var val = parameters != null ? parameters.FirstOrDefault(x => p.ParameterType.IsInstanceOfType(x)) : null;
                    if (val != null)
                    {
                        cargs[i] = val;
                    }
                    else if (vault != null)
                    {
                        cargs[i] = Resolve(services, scope, vault).First();
                    }
                    else if (p.HasDefaultValue)
                    {
                        cargs[i] = p.DefaultValue;
                    }
                    else
                    {
                        var instance = Create(services, scope, p.ParameterType);
                        cargs[i] = instance;
                    }
                }
                var fc = FastMethod.GetMethod(qc.c);
                var result = fc.Invoke(null, cargs);
                if (scope != null)
                    scope[type] = result;
                if (result is ICreateCallback)
                    createSession.Add((ICreateCallback)result);
                ResolveProperties(services, scope, result);
                return result;
            }
            finally
            {
                if (first)
                {
                    for (int i = 0; i < createSession.Count; i++)
                        ((ICreateCallback)createSession[i]).OnCreated();
                    createSession = null;
                }
            }
        }
        [ThreadStatic]
        static List<ICreateCallback> createSession;

        public static bool CanCreate(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
            => FindConstructors(services, scope, type).Any();

        /// <summary>
        /// Return the list of constructor for a type that can be instantiated with the Registry, 
        /// i.e. whose parameters are registered or instantiable.
        /// </summary>
        public static IEnumerable<ConstructorInfo> FindConstructors(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
        {
            if (FindSession == null)
                FindSession = new Stack<Type>();
            if (FindSession.Contains(type))
                yield break;
            FindSession.Push(type);
            try
            {
                if (!CanBeInstantiated(type))
                    yield break;
                var ctors = type.GetTypeInfo().DeclaredConstructors;
                foreach (var c in ctors)
                {
                    bool ok = true;
                    foreach (var p in c.GetParameters())
                    {
                        var impa = p.GetCustomAttribute<ImportAttribute>();
                        var t = (impa != null ? impa.ImportedType : null) ?? p.ParameterType;
                        if (t.IsValueType)
                            continue;
                        var vault = MergeKeys(services, scope, t).Where(x => x != t).FirstOrDefault();
                        if (vault != null)
                            continue;
                        if (p.HasDefaultValue)
                            continue;
                        if (CanCreate(services, scope, p.ParameterType))
                            continue;
                        ok = false;
                        break;
                    }
                    if (ok)
                        yield return c;
                }
            }
            finally
            {
                FindSession.Pop();
            }
        }
        [ThreadStatic]
        static Stack<Type> FindSession;

    }
}
