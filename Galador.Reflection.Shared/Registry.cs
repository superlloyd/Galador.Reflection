using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
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
    /// When one resolve a type (i.e. create an instance with <see cref="Registry.Resolve(Type, LifetimeScope)"/> or <see cref="Registry.Create(Type, LifetimeScope)"/>), 
    /// all their property marked with this attribute will also be set using <see cref="Registry.Resolve(Type, LifetimeScope)"/>
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
    /// This class is a IServiceProvider, IOC, and MEF clone all together.
    /// Services are registered explicitly with <see cref="Register(Type, object)"/> or automatically with <see cref="RegisterAssemblies(Assembly[])"/> assembly.
    /// Then service can then be acquired / resolved, using constructor and property injection lazily with <see cref="Resolve(Type, LifetimeScope)"/>.
    /// Each registered service will be created only once. Imported object (with <see cref="ImportAttribute"/>) which are not registered as service will
    /// be created, only once per request, with <see cref="Create(Type, LifetimeScope)"/> and stored in the <see cref="LifetimeScope"/> for reuse during the request
    /// as it traverses the object dependencies tree.
    /// </summary>
    public sealed class Registry : IDisposable, IServiceProvider
    {
        TypeTree<ServiceDefinition> services = new TypeTree<ServiceDefinition>();
        TypeTree<object> scope = new TypeTree<object>();

        #region ctor() Dispose()

        /// <summary>
        /// Initializes a new instance of the <see cref="Registry"/> class.
        /// </summary>
        public Registry()
        {
            this.services = new TypeTree<ServiceDefinition>();
            this.scope = new TypeTree<object>();
            services[typeof(Registry)] = new ServiceDefinition(this);
        }

        public Registry(TypeTree<ServiceDefinition> services, TypeTree<object> scope)
        {
            this.services = services ?? new TypeTree<ServiceDefinition>();
            this.scope = scope ?? new TypeTree<object>();
            services[typeof(Registry)] = new ServiceDefinition(this);
        }

        /// <summary>
        /// Dispose of all <see cref="IDisposable"/> object registered with the registry and make it unusable.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (var item in scope.GetKeyValues(typeof(object)).Select(x => x.value).OfType<IDisposable>())
                item.Dispose();
            scope.Clear();
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
        /// Registers all typed tagged with attribute <typeparamref name="T"/> within all the known assemblies.
        /// </summary>
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
            foreach (var ti in assemblies.Where(x => x != null).SelectMany(x => x.DefinedTypes))
            {
                var ea = ti.GetCustomAttributes<T>(false);
                foreach (var export in ea)
                {
                    var t = ti.AsType();
                    if (!TypeTreeActivation.CanBeInstantiated(t))
                    {
                        Log.Warning(this, $"[Registry]: Type {t.Name} can't be exported, it is not Resolvable.");
                        continue;
                    }
                    Register(t);
                    break; // register only once
                }
            }
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
            TypeTreeActivation.Register(services, facade, instance);
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
        public bool IsRegistered(Type type) { return services.ContainsKey(type); }

        #endregion

        object IServiceProvider.GetService(Type serviceType) { return Resolve(serviceType); }

        /// <summary>
        /// Create another registry with the same service definition, but none of the automatically created instances.
        /// </summary>
        public Registry CreateScope() => new Registry(services, null);

        public void ResolveProperties(object instance) => TypeTreeActivation.ResolveProperties(services, scope, instance);

        public T Resolve<T>() { return (T)ResolveAll(typeof(T)).First(); }

        public object Resolve(Type type)
        {
            EnsureAlive();
            return TypeTreeActivation.ResolveSingle(services, scope, type);
        }

        public IEnumerable<T> ResolveAll<T>() { return ResolveAll(typeof(T)).OfType<T>(); }

        public IEnumerable<object> ResolveAll(Type type)
        {
            EnsureAlive();
            return TypeTreeActivation.Resolve(services, scope, type);
        }

        public IEnumerable<object> ResolveAll(Predicate<Type> matching)
        {
            EnsureAlive();
            return TypeTreeActivation.Resolve(services, scope, matching);
        }


        /// <summary>
        /// Whether given type can be created. Basically it must be a concrete class with some constructors
        /// with arguments that can also be created (or resolved).
        /// </summary>
        /// <typeparam name="T">The type to check for creation.</typeparam>
        /// <param name="cache">A cache of instance to possibly use for construction.</param>
        /// <returns>Whether the type can be instantiated</returns>
        public bool CanCreate<T>() { return CanCreate(typeof(T)); }

        /// <summary>
        /// Whether given type can be created. Basically it must be a concrete class with some constructors
        /// with arguments that can also be created (or resolved).
        /// </summary>
        /// <param name="type">The type to check for creation.</param>
        /// <param name="cache">A cache of instance to possibly use for construction.</param>
        /// <returns>Whether the type can be instantiated</returns>
        public bool CanCreate(Type type) => TypeTreeActivation.CanCreate(services, scope, type);

        /// <summary>
        /// Create that object from scratch regardless of registration
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <param name="cache">A cache of possible instance to use as property, constructor argument and the like, on top of registered services.</param>
        /// <returns>A newly created instance</returns>
        /// <exception cref="InvalidOperationException">If no appropriate constructor can be found.</exception>
        public T Create<T>() { return (T)Create(typeof(T)); }

        public T Create<T>(params object[] parameters) { return (T)Create(typeof(T), null, parameters); }

        /// <summary>
        /// Create that object from scratch regardless of registration
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <param name="cache">A cache of possible instance to use as property, constructor argument and the like, on top of registered services.</param>
        /// <returns>A newly created instance</returns>
        /// <exception cref="InvalidOperationException">If no appropriate constructor can be found.</exception>
        public object Create(Type type, params object[] parameters) => TypeTreeActivation.Create(services, scope, type, parameters);
    }
}
