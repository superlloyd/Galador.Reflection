using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Galador.Reflection.Utils
{
    [DebuggerDisplay("({Type?.FullName}, {Instance})")]
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

        public static IEnumerable<Type> MergeKeys(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
        {
            IEnumerable<Type> e1, e2;
            if (type == typeof(object) || type == null)
            {
                e1 = services?.GetKeys();
                e2 = scope?.GetKeys();
            }
            else
            {
                e1 = services?.GetKeys(type);
                e2 = scope?.GetKeys(type);
            }
            return (e1 ?? Array.Empty<Type>())
                .Concat(e2 ?? Array.Empty<Type>())
                .Distinct();
        }

        public static IEnumerable<Type> MergeKeys(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Predicate<Type> matching)
            => MergeKeys(services, scope, typeof(object))
            .Where(x => matching(x))
            .Distinct();

        public static object ResolveSingle(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type)
        {
            Type descendant = null;
            foreach (var key in MergeKeys(services, scope, type))
            {
                if (descendant == null)
                {
                    descendant = key;
                }
                else
                {
                    throw new InvalidOperationException($"Resolution ambiguous, multiple descendant found for {type}");
                }
            }

            return SolveExact(services, scope, descendant ?? type);
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
                if (!root.IsValueType)
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

            if (scope.ContainsKey(type))
            {
                return scope[type];
            }
            else
            {
                var value = Create(services, scope, type);
                if (!type.IsValueType)
                    scope[type] = value;
                return value;
            }
        }

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
                    var o = ResolveSingle(services, scope, pi.ImportedType ?? p.Type.Type);
                    item.p.SetValue(instance, o);
                }
            }
        }

        public static object Create(ITypeTree<ServiceDefinition> services, ITypeTree<object> scope, Type type, params object[] parameters)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);

            if (scope == null)
                scope = new TypeTree<object>();

            bool first = createSession == null;
            if (first)
                createSession = new List<ICreateCallback>();
            try
            {
                // private constructor are private for a reason, don't use them!
                var ctor = FindConstructors(services, scope, type).FirstOrDefault(x => x.IsPublic);
                if (ctor == null)
                    throw new InvalidOperationException($"Type {type.FullName} can't be Activated, no constructor can be called at this time.");

                var pis = ctor.GetParameters();
                var cargs = new object[pis.Length];
                for (int i = 0; i < pis.Length; i++)
                {
                    var p = pis[i];
                    var impa = p.GetCustomAttribute<ImportAttribute>();
                    var t = (impa != null ? impa.ImportedType : null) ?? p.ParameterType;
                    var descendant = MergeKeys(services, scope, t).Where(x => x != type).FirstOrDefault();
                    var val = parameters != null ? parameters.FirstOrDefault(x => t.IsInstanceOfType(x)) : null;
                    if (val != null)
                    {
                        cargs[i] = val;
                    }
                    else if (descendant != null)
                    {
                        cargs[i] = SolveExact(services, scope, descendant);
                    }
                    else if (p.HasDefaultValue)
                    {
                        cargs[i] = p.DefaultValue;
                    }
                    else if (CanBeInstantiated(t))
                    {
                        cargs[i] = Resolve(services, scope, t).FirstOrDefault();
                    }
                }
                var fc = FastMethod.GetMethod(ctor);
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
            if (FindSession.Contains(type) || !CanBeInstantiated(type))
                return Array.Empty<ConstructorInfo>();
            FindSession.Push(type);
            try
            {
                return
                    from c in type.GetTypeInfo().DeclaredConstructors
                    let parameters = c.GetParameters()
                    where parameters.All(x => CanCreateParameter(x))
                    let N = parameters.Length
                    let difficulty = parameters.Select(x => IsKnownParameter(x) ? 0 : 1).Sum()
                    orderby difficulty ascending, N descending
                    select c
                ;

                bool CanCreateParameter(ParameterInfo pi)
                {
                    var impa = pi.GetCustomAttribute<ImportAttribute>();
                    var t = (impa != null ? impa.ImportedType : null) ?? pi.ParameterType;
                    if (t.IsValueType)
                        return true;
                    var descendant = MergeKeys(services, scope, t).Where(x => x != type).FirstOrDefault();
                    if (descendant != null)
                        return true;
                    if (pi.HasDefaultValue)
                        return true;
                    if (CanCreate(services, scope, pi.ParameterType))
                        return true;
                    return false;
                }

                bool IsKnownParameter(ParameterInfo pi)
                {
                    var impa = pi.GetCustomAttribute<ImportAttribute>();
                    var t = (impa != null ? impa.ImportedType : null) ?? pi.ParameterType;
                    var descendant = MergeKeys(services, scope, t).Where(x => x != type).FirstOrDefault();
                    if (descendant != null)
                        return true;
                    return false;
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
