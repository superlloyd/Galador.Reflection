using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using Galador.Reflection.Logging;
using Galador.Reflection.Utils;

namespace Galador.Reflection
{
    #region ExportAttribute, ImportAttribute

    /// <summary>
    /// Out of the box hleper attribute for automatic registration with <see cref="Registry.RegisterAssemblies(Assembly[])"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportAttribute : Attribute
    {
    }

    /// <summary>
    /// When one resolve a type (i.e. create an instance with <see cref="Registry.Resolve(Type, RequestCache)"/> or <see cref="Registry.Create(Type, RequestCache)"/>), 
    /// all their property marked with this attribute will also be set using <see cref="Registry.Resolve(Type, RequestCache)"/>
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
    /// Registered service instances are stored in the registry that register them.
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
    /// Then service can then be acquired / resolved, using constructor and property injection lazily with <see cref="Resolve(Type, RequestCache)"/>.
    /// Each registered service will be created only once. Imported object (with <see cref="ImportAttribute"/>) which are not registered as service will
    /// be created, only once per request, with <see cref="Create(Type, RequestCache)"/> and stored in the <see cref="RequestCache"/> for reuse during the request
    /// as it traverses the object dependencies tree.
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
        /// </summary>
        public Registry()
            : this(false)
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

        public void RegisterAssemblies<T>()
            where T: Attribute
        {
            RegisterAssemblies<T>(KnownAssemblies.Current);
        }

        /// <summary>
        /// Register some assembly, by registering all type they export that are marked with <see cref="ExportAttribute"/>.
        /// </summary>
        public void RegisterAssemblies<T>(params Assembly[] ass)
            where T : Attribute
        {
            RegisterAssemblies<T>((IEnumerable<Assembly>)ass);
        }

        /// <summary>
        /// Register some assembly, by registering all type they export that are marked with <see cref="ExportAttribute"/>.
        /// </summary>
        public void RegisterAssemblies<T>(IEnumerable<Assembly> assemblies)
            where T : Attribute
        {
            ForEach(assemblies.Where(x => x != null).SelectMany(x => x.DefinedTypes),
            ti =>
            {
                var ea = ti.GetCustomAttributes<T>(false);
                foreach (var export in ea)
                {
                    var t = ti.AsType();
                    if (!CanBeInstantiated(t))
                    {
                        TraceKeys.Registry.Warning($"[Registry]: Type {t.Name} can't be exported, it is not Resolvable.");
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
        /// Register all arguments types with the registry
        /// </summary>
        /// <param name="types">All type to register</param>
        public void Register(params Type[] types)
        {
            if (types == null)
                return;
            foreach (var t in types)
            {
                if (t == null)
                    continue;
                Register(t, (object)null);
            }
        }

        /// <summary>
        /// Register an instance as an instance of a given type. It will returned when resolving any
        /// base class or implemented interface.
        /// </summary>
        /// <param name="facade">The type that is registered.</param>
        /// <param name="instance">The instance for this type. If null it will be created on demand 
        /// on the first access to <paramref name="facade"/></param>
        public void Register(Type facade, object instance)
        {
            EnsureAlive();
            if (facade == null)
                throw new ArgumentNullException(nameof(facade));
            if (instance != null && !Registry.IsInstance(facade, instance))
                throw new ArgumentException($"{instance} is not a {facade.Name}");
            if (instance == null && !CanBeInstantiated(facade))
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

        object IServiceProvider.GetService(Type serviceType) { return Resolve(serviceType); }

        #region Resolve(All)()

        /// <summary>
        /// This will resolve the first <typeparamref name="T"/> with <see cref="ResolveAll(Type, RequestCache)"/>
        /// </summary>
        /// <typeparam name="T">The requested type</typeparam>
        /// <param name="cache">A cache for all instance that need be create and are not a registered service. Can be null</param>
        /// <returns>An instance of <typeparamref name="T"/>, either a reused service if registered, or newly created instance otherwise.</returns>
        public T Resolve<T>(RequestCache cache = null) { return (T)ResolveAll(typeof(T), (RequestCache)null).First(); }
        /// <summary>
        /// This will resolve the first <paramref name="type"/> with <see cref="ResolveAll(Type, RequestCache)"/>
        /// </summary>
        /// <param name="type">The requested type</param>
        /// <param name="cache">A cache for all instance that need be create and are not a registered service. Can be null</param>
        /// <returns>An instance of type <paramref name="type"/>, either a reused service if registered, or newly created instance otherwise.</returns>
        public object Resolve(Type type, RequestCache cache = null) { return ResolveAll(type, cache).First(); }

        /// <summary>
        /// Resolve all registered service that inherit from, or implement or are <typeparamref name="T"/>.
        /// If none are registered, it will create and return newly create instance of <typeparamref name="T"/>
        /// , that will be saved in the <paramref name="cache"/>.
        /// </summary>
        /// <typeparam name="T">The requested type</typeparam>
        /// <param name="cache">Where newly create instance that are not service are cached for reuse.</param>
        /// <returns>Al registered service which implement or are subclass of <typeparamref name="T"/> or a newly create one.</returns>
        public IEnumerable<T> ResolveAll<T>(RequestCache cache = null) { return ResolveAll(typeof(T), cache).Cast<T>(); }

        /// <summary>
        /// Resolve all registered service that inherit from, or implement or are <paramref name="type"/>.
        /// If none are registered, it will create and return newly create instance of <paramref name="type"/>
        /// , that will be saved in the <paramref name="cache"/>.
        /// </summary>
        /// <param name="type">The requested type</param>
        /// <param name="cache">Where newly create instance that are not service are cached for reuse.</param>
        /// <returns>Al registered service which implement or are subclass of <paramref name="type"/> or a newly create one.</returns>
        public IEnumerable<object> ResolveAll(Type type, RequestCache cache = null)
        {
            if (cache == null)
                cache = new RequestCache();
            var none = true;
            foreach (var vault in FindRegistrations(type, cache))
            {
                none = false;
                Resolve(vault, cache);
                yield return vault.Instance;
            }
            if (none)
            {
                if (!CanCreate(type, cache))
                    throw new InvalidOperationException($"Can't create {type.FullName}");
                var instance = Create(type, cache);
                yield return instance;
            }
        }
        object Resolve(TypeVault vault, RequestCache cache)
        {
            if (vault.Instance == null)
                vault.Instance = Create(vault.Type, cache);
            return vault.Instance;
        }

        #endregion

        #region ResolveProperties()

        /// <summary>
        /// Will set all properties marked with <see cref="ImportAttribute"/>. If property is an array or a <see cref="List{T}"/>
        /// It will <see cref="ResolveAll(Type, RequestCache)"/> and set up the array / collection with the result.
        /// </summary>
        /// <param name="instance">The object which property must be resolved.</param>
        /// <param name="cache">A cache for the request so that each instance which are not service is created only once.</param>
        public void ResolveProperties(object instance, RequestCache cache = null)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (cache == null)
                cache = new RequestCache();

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
                        var objs = FindRegistrations(t, cache).Select(x => Resolve(x, cache)).ToList();
                        var prop = Array.CreateInstance(t, objs.Count);
                        for (int i = 0; i < objs.Count; i++)
                            prop.SetValue(objs[i], i);
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
                            value = Create(p.Type.Type, cache);
                            p.SetValue(instance, value);
                        }
                        var list = (IList)value;
                        ForEach(FindRegistrations(t, cache).Select(x => Resolve(x, cache)), x => list.Add(x));
                    }
                }
                else
                {
                    // simple property
                    var o = ResolveAll(pi.ImportedType ?? p.Type.Type, cache).First();
                    item.p.SetValue(instance, o);
                }
            }
        }

        #endregion

        #region CanCreate() Create() FindConstructors()

        /// <summary>
        /// Whether given type can be created. Basically it must be a concrete class with some constructors
        /// with arguments that can also be created (or resolved).
        /// </summary>
        /// <typeparam name="T">The type to check for creation.</typeparam>
        /// <param name="cache">A cache of instance to possibly use for construction.</param>
        /// <returns>Whether the type can be instantiated</returns>
        public bool CanCreate<T>(RequestCache cache = null) { return CanCreate(typeof(T), cache); }

        /// <summary>
        /// Whether given type can be created. Basically it must be a concrete class with some constructors
        /// with arguments that can also be created (or resolved).
        /// </summary>
        /// <param name="type">The type to check for creation.</param>
        /// <param name="cache">A cache of instance to possibly use for construction.</param>
        /// <returns>Whether the type can be instantiated</returns>
        public bool CanCreate(Type type, RequestCache cache = null) 
        {
            return FindConstructors(type, cache).Any();
        }

        /// <summary>
        /// Create that object from scratch regardless of registration
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <param name="cache">A cache of possible instance to use as property, constructor argument and the like, on top of registered services.</param>
        /// <returns>A newly created instance</returns>
        /// <exception cref="InvalidOperationException">If no appropriate constructor can be found.</exception>
        public T Create<T>(RequestCache cache = null) { return (T)Create(typeof(T), cache); }


        /// <summary>
        /// Create that object from scratch regardless of registration
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <param name="cache">A cache of possible instance to use as property, constructor argument and the like, on top of registered services.</param>
        /// <returns>A newly created instance</returns>
        /// <exception cref="InvalidOperationException">If no appropriate constructor can be found.</exception>
        public object Create(Type type, RequestCache cache = null)
        {
            bool first = createSession == null;
            if (first)
                createSession = new List<IRegistryDelegate>();
            if (cache == null)
                cache = new RequestCache();
            try
            {
                var ctors = FindConstructors(type, cache);
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
                    var vault = FindRegistrations(t, cache).FirstOrDefault(x => x.Instance != null || x.Type != type);
                    if (vault != null)
                    {
                        Resolve(vault, cache);
                        cargs[i] = vault.Instance;
                    }
                    else if (p.HasDefaultValue)
                    {
                        cargs[i] = p.DefaultValue;
                    }
                    else
                    {
                        var instance = Create(p.ParameterType, cache);
                        cargs[i] = instance;
                    }
                }
                var fc = FastMethod.GetMethod(qc.c);
                var result = fc.Invoke(null, cargs);
                cache[type] = result;
                if (result is IRegistryDelegate)
                    createSession.Add((IRegistryDelegate)result);
                ResolveProperties(result, cache);
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
        IEnumerable<ConstructorInfo> FindConstructors(Type type, RequestCache cache)
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
                        var vault = FindRegistrations(t, cache).FirstOrDefault(x => x.Instance != null || x.Type != type);
                        if (vault != null)
                            continue;
                        if (p.HasDefaultValue)
                            continue;
                        if (CanCreate(p.ParameterType, cache))
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

        IEnumerable<TypeVault> FindRegistrations(Type type, RequestCache cache)
        {
            if (cache != null)
            {
                var cvault = cache.GetVault(type);
                if (cvault != null)
                    yield return cvault;
            }

            EnsureAlive();
            List<TypeVault> list;
            if (!reverseInheritance.TryGetValue(type, out list))
                yield break;
            foreach (var vault in list)
                yield return vault;
        }

        #endregion

        #region static utils: CanBeInstantiated() ForEach() IsBaseClass() IsInstance() GetInheritanceTree()

        static bool CanBeInstantiated(Type type)
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
