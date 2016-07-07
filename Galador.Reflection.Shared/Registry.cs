using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using Galador.Reflection.Logging;

namespace Galador.Reflection
{
    #region ExportAttribute, ImportAttribute

    /// <summary>
    /// Type marked with this attribute are the one that will be registered when one register their assembly with <see cref="Registry.RegisterAssemblies(Assembly[])"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportAttribute : Attribute
    {
    }

    /// <summary>
    /// When one resolve a type (i.e. create an instance with <see cref="Registry.Resolve(Type, Registry[])"/> or <see cref="Registry.Create{T}(Registry)"/>), 
    /// all their property marked with this attribute will also be set using <see cref="Registry.Resolve{T}(Registry[])"/>
    /// Can also be used on constructor parameter to specify a particular type to use.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
    public class ImportAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportAttribute"/> class.
        /// </summary>
        /// <param name="eType">(Optional) type that is imported. Will set <see cref="ImportedType"/></param>
        public ImportAttribute(Type eType = null)
        {
            ImportedType = eType;
        }
        /// <summary>
        /// The required type to be imported. If null the type of the member or constructor parameter will be used.
        /// </summary>
        public Type ImportedType { get; set; }
    }

    #endregion

    /// <summary>
    /// Delegate called after object creation
    /// </summary>
    public interface IRegistryDelegate
    {
        /// <summary>
        /// Called when the full object tree (i.e. with all dependency) has been created.
        /// </summary>
        void OnRegistryCreated();
    }

    #region RequestCache

    /// <summary>
    /// All unregistered instances created during a request are stored here.
    /// It can be reused for later queries, creating some sort of session lifespan for temporary object.
    /// <br/>
    /// Instances are indexed by type since the registry will only create one instance of each type (for a given cache),
    /// reusing this instance for all subsequent query.
    /// </summary>
    public class RequestCache : IDisposable
    {
        Dictionary<Type, Registry.TypeVault> values = new Dictionary<Type, Registry.TypeVault>();

        /// <summary>
        /// Will dispose of all <see cref="IDisposable"/> that it contains
        /// </summary>
        public void Dispose()
        {
            foreach (var item in values.Values.Select(x => x.Instance).OfType<IDisposable>())
                item.Dispose();
            values.Clear();
        }

        /// <summary>
        /// Gets the instance of given type that has been created, or null.
        /// </summary>
        /// <param name="index">The type of the instance.</param>
        /// <returns>An already created and cached instance, or null</returns>
        public object this[Type index]
        {
            get
            {
                Registry.TypeVault result;
                if (values.TryGetValue(index, out result))
                    return result.Instance;
                return null;
            }
            internal set
            {
                Registry.TypeVault result;
                if (!values.TryGetValue(index, out result))
                {
                    result = new Registry.TypeVault(index);
                    values[index] = result;
                }
                result.Instance = value;
            }
        }
        internal Registry.TypeVault GetVault(Type index)
        {
            Registry.TypeVault result;
            values.TryGetValue(index, out result);
            return result;
        }

        /// <summary>
        /// The number of cached instance.
        /// </summary>
        public int Count { get { return values.Count; } }

        /// <summary>
        /// All the type for which an instance has been created and cached.
        /// </summary>
        public IEnumerable<Type> Keys { get { return values.Keys; } }

        /// <summary>
        /// Whether an instance of this exact type has been created.
        /// </summary>
        public bool Contains(Type key) { return values.ContainsKey(key); }
    }

    #endregion

    /// <summary>
    /// This class is a IServiceProvider, IOC, and MEF clone all together.
    /// Services are registered explicitly with <see cref="Register(Type, object)"/> or automatically with <see cref="RegisterAssemblies(Assembly[])"/> assembly.
    /// Then service can then be resolved, using constructor and property injection lazily on <see cref="Resolve(Type, RequestCache, Registry[])"/>.
    /// </summary>
    public class Registry : IDisposable, IServiceProvider
    {
        internal class TypeVault
        {
            public TypeVault(Type type)
            {
                this.Type = type;
            }
            public readonly Type Type;
            public object Instance;
            public IEnumerable<Type> Implements() => GetInheritanceTree(Type).Distinct();
        }
        Dictionary<Type, TypeVault> exact = new Dictionary<Type, TypeVault>();
        Dictionary<Type, List<TypeVault>> reverseInheritance = new Dictionary<Type, List<TypeVault>>();

        #region ctor() Dispose()

        /// <summary>
        /// Initializes a new instance of the <see cref="Registry"/> class.
        /// And register itself has an exported type.
        /// </summary>
        public Registry()
            : this(true)
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Registry"/> class.
        /// </summary>
        /// <param name="registerSelf">if set to <c>true</c> the registry will register itself and can be injected as a dependency.</param>
        public Registry(bool registerSelf)
        {
            if (registerSelf)
                Register(this);
        }
#pragma warning disable 1591 // XML Comments
        ~Registry() { Dispose(false); }
#pragma warning restore 1591 // XML Comments

        /// <summary>
        /// Dispose of all <see cref="IDisposable"/> object registered with the registry and make it unusable.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
        void Dispose(bool disposing)
        {
            if (disposed)
                return;
            disposed = true;
            if (disposing)
            {
                List<TypeVault> disposables;
                if (reverseInheritance.TryGetValue(typeof(IDisposable), out disposables))
                {
                    foreach (var item in disposables)
                    {
                        var instance = (IDisposable)item.Instance;
                        if (instance != null)
                            instance.Dispose();
                    }
                }
                exact = null;
                reverseInheritance = null;
            }
        }
        bool disposed;
        void EnsureAlive() { if (disposed) throw new ObjectDisposedException(GetType().Name); }

        /// <summary>
        /// Whether this registry has been disposed or not.
        /// </summary>
        public bool IsDisposed { get { return disposed; } }

        #endregion

        #region RegisterAssemblies()

        /// <summary>
        /// Register some assembly, by registering all type they export that are marked with <see cref="ExportAttribute"/>.
        /// </summary>
        public void RegisterAssemblies(params Assembly[] ass) { RegisterAssemblies((IEnumerable<Assembly>)ass); }

        /// <summary>
        /// Register some assembly, by registering all type they export that are marked with <see cref="ExportAttribute"/>.
        /// </summary>
        public void RegisterAssemblies(IEnumerable<Assembly> assemblies)
        {
            ForEach(assemblies.Where(x => x != null).SelectMany(x => x.DefinedTypes),
            ti =>
            {
                var ea = ti.GetCustomAttributes<ExportAttribute>(false);
                foreach (var export in ea)
                {
                    var t = ti.AsType();
                    if (!IsResolvable(t))
                    {
                        TraceKeys.IoC.Warning($"[Registry]: Type {t.Name} can't be exported, it is not Resolvable.");
                        continue;
                    }
                    Register(t);
                }
            });
        }

        #endregion

        #region Register() IsRegistered()

        /// <summary>
        /// Register <typeparamref name="T"/> as an exported type. 
        /// <br/><see cref="Register(Type, object)"/>.
        /// </summary>
        /// <typeparam name="T">The type that is registered.</typeparam>
        public void Register<T>() where T : class { Register(typeof(T), null); }
        /// <summary>
        /// Register <typeparamref name="T"/> as an exported type. 
        /// <br/><see cref="Register(Type, object)"/>.
        /// </summary>
        /// <typeparam name="T">The type that is registered.</typeparam>
        /// <param name="instance">The instance that is set against this type.</param>
        public void Register<T>(T instance) where T : class { Register(typeof(T), instance); }
        /// <summary>
        /// Register <paramref name="facade"/> as an exported type. 
        /// <br/><see cref="Register(Type, object)"/>.
        /// </summary>
        /// <param name="facade">The type that is registered as a service.</param>
        public void Register(Type facade) { Register(facade, null); }
        public void Register(Type facade, object instance)
        {
            EnsureAlive();
            if (facade == null)
                throw new ArgumentNullException(nameof(facade));
            if (instance != null && !Registry.IsInstance(facade, instance))
                throw new ArgumentException($"{instance} is not a {facade.Name}");
            if (instance == null && !IsResolvable(facade))
                throw new ArgumentNullException(nameof(instance), $"Type {facade.Name} is not Resolvable, registered instance should NOT be null.");
            TypeVault vault;
            if (!exact.TryGetValue(facade, out vault))
            {
                vault = new TypeVault(facade);
                exact[facade] = vault;

                foreach (var implements in vault.Implements())
                {
                    List<TypeVault> list;
                    if (!reverseInheritance.TryGetValue(implements, out list))
                    {
                        list = new List<TypeVault>();
                        reverseInheritance[implements] = list;
                    }
                    list.Add(vault);
                }
            }
            vault.Instance = instance;
        }

        /// <summary>
        /// Return whether there is an object that is registered for the requested type.
        /// </summary>
        /// <typeparam name="T">The type to check for registration.</typeparam>
        /// <returns>Whether the given type is registered</returns>
        public bool IsRegistered<T>() { return IsRegistered(typeof(T)); }
        /// <summary>
        /// Return whether there is an object that is registered for the requested type.
        /// </summary>
        /// <param name="type">The type to check for registration.</param>
        /// <returns>Whether the given type is registered</returns>
        public bool IsRegistered(Type type) { return reverseInheritance.ContainsKey(type); }

        #endregion

        /// <summary>
        /// Shared Registry to be used for static resolution purpose
        /// </summary>
        public static Registry Shared { get; } = new Registry();

        object IServiceProvider.GetService(Type serviceType) { return Resolve(serviceType); }

        #region Resolve(All)()

        public T Resolve<T>(RequestCache requestCache = null) { return ResolveAll(typeof(T), (RequestCache)null, this, Shared).Cast<T>().First(); }
        public object Resolve(Type t, RequestCache requestCache = null) { return ResolveAll(t, requestCache, this, Shared).Cast<object>().First(); }

        public IEnumerable<T> ResolveAll<T>(RequestCache requestCache = null) { return ResolveAll(typeof(T), requestCache, this, Shared).Cast<T>(); }
        public IEnumerable ResolveAll(Type type, RequestCache requestCache = null) { return ResolveAll(type, requestCache, this, Shared); }

        public static T Resolve<T>(params Registry[] registries) { return ResolveAll(typeof(T), (RequestCache)null, registries).Cast<T>().First(); }
        public static object Resolve(Type t, params Registry[] registries) { return ResolveAll(t, (RequestCache)null, registries).Cast<object>().First(); }
        public static object Resolve(Type t, RequestCache cache, params Registry[] registries) { return ResolveAll(t, cache, registries).Cast<object>().First(); }

        public static IEnumerable<T> ResolveAll<T>(params Registry[] registries) { return ResolveAll(typeof(T), (RequestCache)null, registries).Cast<T>(); }
        public static IEnumerable<T> ResolveAll<T>(RequestCache cache, params Registry[] registries) { return ResolveAll(typeof(T), cache, registries).Cast<T>(); }
        public static IEnumerable ResolveAll(Type type, params Registry[] registries) { return ResolveAll(type, (RequestCache)null, registries); }
        public static IEnumerable ResolveAll(Type type, RequestCache cache, params Registry[] registries)
        {
            if (cache == null)
                cache = new RequestCache();
            var none = true;
            foreach (var vault in FindRegistrations(type, cache, registries))
            {
                none = false;
                Resolve(vault, cache, registries);
                yield return vault.Instance;
            }
            if (none)
            {
                if (!CanCreate(type, cache, registries))
                    throw new InvalidOperationException($"Can't create {type.FullName}");
                var instance = Create(type, cache, registries);
                yield return instance;
            }
        }
        static object Resolve(TypeVault vault, RequestCache cache, params Registry[] registries)
        {
            if (vault.Instance == null)
                vault.Instance = Create(vault.Type, cache, registries);
            return vault.Instance;
        }

        #endregion

        #region ResolveProperties()

        public void ResolveProperties(object result, RequestCache cache = null) { ResolveProperties(result, cache, this, Shared); }
        public static void ResolveProperties(object result, params Registry[] registries) { ResolveProperties(result, (RequestCache)null, registries); }
        public static void ResolveProperties(object result, RequestCache cache, params Registry[] registries)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (cache == null)
                cache = new RequestCache();

            var props = (
                from p in result.GetType().GetRuntimeProperties()
                let pi = p.GetCustomAttributes<ImportAttribute>().FirstOrDefault()
                where pi != null
                select new { p, pi }
            ).ToList();
            foreach (var item in props)
            {
                var p = item.p;
                var pi = item.pi;
                if (IsBaseClass(typeof(IEnumerable), p.PropertyType))
                {
                    var t = pi.ImportedType;
                    // T[]
                    if (p.PropertyType.IsArray)
                    {
                        if (t == null)
                            t = p.PropertyType.GetElementType();
                        if (!IsBaseClass(p.PropertyType.GetElementType(), t))
                            throw new NotSupportedException($"Property {result.GetType().Name}.{p.Name}, can't import {t.Name}");
                        var objs = FindRegistrations(t, cache, registries).Select(x => Resolve(x, cache, registries)).ToList();
                        var prop = Array.CreateInstance(t, objs.Count);
                        for (int i = 0; i < objs.Count; i++)
                            prop.SetValue(objs[i], i);
                        item.p.SetValue(result, prop);
                    }
                    // List<T>
                    else
                    {
                        if (!IsBaseClass(typeof(IList), p.PropertyType))
                            throw new InvalidOperationException($"[Import] property {result.GetType().Name}.{p.Name} must be an array or an IList");

                        if (t == null)
                        {
                            var ga = p.PropertyType.GenericTypeArguments;
                            if (ga.Length != 1)
                                throw new NotSupportedException($"[Import] property {result.GetType().Name}.{p.Name} must be generic or the Import type must be defined");
                            t = ga[0];
                        }
                        var value = p.GetValue(result);
                        if (value == null)
                        {
                            if (!IsResolvable(p.PropertyType))
                                throw new InvalidOperationException($"Can't [Import]{p.PropertyType.Name} for {result.GetType().Name}.{p.Name}");
                            value = Create(p.PropertyType, cache, registries);
                            p.SetValue(result, value);
                        }
                        var list = (IList)value;
                        ForEach(FindRegistrations(t, cache, registries).Select(x => Resolve(x, cache, registries)), x => list.Add(x));
                    }
                }
                else
                {
                    // simple property
                    var o = Resolve(pi.ImportedType ?? p.PropertyType, cache, registries);
                    item.p.SetValue(result, o);
                }
            }
        }

        #endregion

        #region CanCreate() Create() FindConstructors()

        public bool CanCreate<T>(RequestCache cache = null) { return CanCreate(typeof(T), cache, this, Shared); }
        public bool CanCreate(Type type, RequestCache cache = null) { return CanCreate(type, cache, this, Shared); }
        public static bool CanCreate<T>(params Registry[] registries) { return CanCreate(typeof(T), (RequestCache)null, registries); }
        public static bool CanCreate(Type type, params Registry[] registries) { return CanCreate(type, (RequestCache)null, registries); }
        public static bool CanCreate(Type type, RequestCache cache, params Registry[] registries)
        {
            return FindConstructors(type, cache, registries).Any();
        }

        public T Create<T>(Registry requestCache = null) { return (T)Create(typeof(T), requestCache, this, Shared); }
        public object Create(Type type, Registry requestCache = null) { return Create(type, requestCache, this, Shared); }

        public static T Create<T>(params Registry[] registries) { return (T)Create(typeof(T), (RequestCache)null, registries); }
        public static T Create<T>(RequestCache cache, params Registry[] registries) { return (T)Create(typeof(T), cache, registries); }

        public static object Create(Type type, params Registry[] registries) { return Create(type, (RequestCache)null, registries); }

        /// <summary>
        /// Create that object from scratch regardless of lifetime or registration
        /// </summary>
        public static object Create(Type type, RequestCache cache, params Registry[] registries)
        {
            bool first = createSession == null;
            if (first)
                createSession = new List<IRegistryDelegate>();
            if (cache == null)
                cache = new RequestCache();
            try
            {
                var ctors = FindConstructors(type, cache, registries);
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
                    var vault = FindRegistrations(t, cache, registries).FirstOrDefault(x => x.Instance != null || x.Type != type);
                    if (vault != null)
                    {
                        Resolve(vault, cache, registries);
                        cargs[i] = vault.Instance;
                    }
                    else if (p.DefaultValue != DBNull.Value)
                    {
                        cargs[i] = p.DefaultValue;
                    }
                    else
                    {
                        var instance = Create(p.ParameterType, cache, registries);
                        cargs[i] = instance;
                    }
                }
                var result = qc.c.Invoke(cargs);
                cache[type] = result;
                if (result is IRegistryDelegate)
                    createSession.Add((IRegistryDelegate)result);
                ResolveProperties(result, cache, registries);
                return result;
            }
            finally
            {
                if (first)
                {
                    ForEach(createSession, x => x.OnRegistryCreated());
                    createSession = null;
                }
            }
        }
        [ThreadStatic]
        static List<IRegistryDelegate> createSession;

        /// <summary>
        /// Return the list of constructor for a type that can be instantiated with the Registry, 
        /// i.e. whose parameters are registered or instantiable.
        /// </summary>
        static IEnumerable<ConstructorInfo> FindConstructors(Type type, RequestCache cache, params Registry[] registries)
        {
            if (FindSession == null)
                FindSession = new Stack<Type>();
            if (FindSession.Contains(type))
                yield break;
            FindSession.Push(type);
            try
            {
                if (!IsResolvable(type))
                    yield break;
                var ctors = type.GetTypeInfo().DeclaredConstructors;
                foreach (var c in ctors)
                {
                    bool ok = true;
                    foreach (var p in c.GetParameters())
                    {
                        var impa = p.GetCustomAttribute<ImportAttribute>();
                        var t = (impa != null ? impa.ImportedType : null) ?? p.ParameterType;
                        var vault = FindRegistrations(t, cache, registries).FirstOrDefault(x => x.Instance != null || x.Type != type);
                        if (vault != null)
                            continue;
                        if (p.DefaultValue != DBNull.Value)
                            continue;
                        if (CanCreate(p.ParameterType, cache, registries))
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

        #endregion

        #region FindRegistrations()

        static IEnumerable<TypeVault> FindRegistrations(Type type, RequestCache cache, params Registry[] registries)
        {
            if (cache != null)
            {
                var cvault = cache.GetVault(type);
                if (cvault != null)
                    yield return cvault;
            }

            if (registries == null)
                yield break;

            for (int i = 0; i < registries.Length; i++)
            {
                var reg = registries[i];
                if (reg == null)
                    continue;

                // don't duplicate
                var isNew = true;
                for (int j = 0; j < i; j++)
                    if (registries[j] == reg)
                    {
                        isNew = false;
                        break;
                    }
                if (!isNew)
                    continue;

                reg.EnsureAlive();
                List<TypeVault> list;
                if (!reg.reverseInheritance.TryGetValue(type, out list))
                    continue;
                foreach (var vault in list)
                    yield return vault;
            }
        }

        #endregion

        #region static utils: IsResolvable() ForEach() IsBaseClass() IsInstance() GetInheritanceTree()

        public static bool IsResolvable(Type type)
        {
            var t = type.GetTypeInfo();
            if (t.IsAbstract || t.IsInterface)
                return false;
            // No structs, makes no sense in the Registry...
            if (!t.IsClass)
                return false;
            return t.DeclaredConstructors.Any();
        }
        static void ForEach<T>(IEnumerable<T> e, Action<T> a)
        {
            foreach (var item in e)
                a(item);
        }
        static bool IsBaseClass(Type parent, Type child)
        {
            return parent.GetTypeInfo().IsAssignableFrom(child.GetTypeInfo());
        }
        internal static bool IsInstance(Type parent, object child)
        {
            if (child == null)
                return parent.GetTypeInfo().IsClass;
            return IsBaseClass(parent, child.GetType());
        }
        static IEnumerable<Type> GetInheritanceTree(Type t)
        {
            var p = t;
            while (p != null)
            {
                yield return p;
                foreach (var item in p.GetTypeInfo().ImplementedInterfaces)
                    foreach (var sub in GetInheritanceTree(item))
                        yield return sub;
                p = p.GetTypeInfo().BaseType;
            }
        }

        #endregion
    }
}
