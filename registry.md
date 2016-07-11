
## Introduction

In my application I needed to be able to dynamically load type and their dependency at runtime. 
My few interaction with various IoC framework / library were not the most successful. Except with MEF, which seemed great at the time!
Except every time I wanted to use it again I completely forget how to set it up and had to Google for a few hours.
Hence this library which is (hopefully) as easy to use as MEF but with almost no setup required.

Enter `Galador.Reflection.Registry`

## Basic Usage
First one can, optionally, register some service classes, either directly by name with `Register()`
or all service marked with `ExportAttribute` in a given assembly.
Note that one must register classes, not interfaces.

    var reg = new Registry();
    reg.Register<MyService>();
    reg.RegisterAssemblies(typeof(OtherService).Assembly);

Those service will be return when any super class or implemented interface is required.

Then one can use the registry as replacement for `Activator`, 
which will also do property and constructor injection.

    class A
    {
        // here svc will be resolve and obj will be created, if possible
        A(IOtherService svc, NotAService obj) { ... }

        [Import]
        public IService MyService { get; set; }
    }
    var a = reg.Create<A>();
    a.MyService.DoSomething();


One can also gain access to registered instance service by calling `Resolve()`

    var svc = reg.Resolve<IService>()
    var disposable = reg.ResolveAll<IDisposable>();

**Remark** If multiple service implement the same interface or are subclass of a common base class,
    `Resolve()` will return a `ResolveAll().First()` which is a random result in that case.

Finally one can always do the property injection anytime with

    var instance = ....
    reg.ResoleProperties(instance);


## Functionality
If one ignore the multiple polymorphic version the registry (and related attributes) basically implement the following methods:

    [AttributeUsage(AttributeTargets.Class)]
    class ExportAttribute : Attribute {} // only use for RegisterAssemblies()

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    class ImportAttribute : Attribute // for property / constructor injection
    {
        public Type ImportedType { get; set; } // if null, use paramter / property type
    }

    class Registry : IServiceProvider
    {
        // register services
        void RegisterAssemblies(exported type in assemblies)
        void Register(types);
        bool IsRegistered(type);

        // resolve (i.e. create only once in a call) any object
        T Resolve<T>();
        IEnumerable<T> ResolveAll<T>();

        // create: make a new one every time
        T Create<T>();
        bool CanCreate<T>();

        // bonus method: resolve property of existing instance
        void ResolveProperties(instance);
    }

**Service**

Service are class registered with the `Registry` and would be singleton when created by that `Registry`.
However Registry can resolve or create any class, not just service.

**Registering:**

The `Registry` maintains a keyed list of all registered service by type. 
Only one service can be registered against a particular class, however it will resolve for all base classes and 
implemented interfaces.
The service instance, if not provided at registration time, will be created the first time it is accessed 
and will be cached by the registry. Later request of that type will returned the same instance.

**Resolving:**

`Resolve()` will simply call `ResolveAll().First()`. `ResolveAll()` will look for all service implementing
    the requested class and/or interface. If none can be found it will `Create()` a new `T` if possible.
If the type is accessed for the first time, it will be created (with `Create()`) and its property will be injected
(with `ResolveProperties()`), which might cause multiple recursive call to `Resolve()`.
If multiple dependencies refer to the same type, an instance, saved in the `RequestCache` will be reused.

**Create:**

This will always create a **new** instance of the requested type. However all dependencies will be shared and reused as with `Resolve()`.
`Create()` will always use the constructor with the most parameters that it can fill, creating them on demand if needed and possible.
`ImportAttribute` can also be used on constructor parameter to specify a particular subclass or interface implementation.
`ResolveProperties()`

**ExportAttribute**

This attribute is only used with `RegisterAssemblies()` to mark type that must be registered as service

**ImportAttribute**

This attribute is used to mark a dependency (that need not be a service). When an object is created all 
its dependency are resolved and their dependencies and so on... One can use to specify a specific subclass to implement.

    class MyClass
    {
        MyClass([Import(typeof(SpecificService))]IService svc) { .. }

        [Import]
        public LocationService Location { get; set; }

        public B B { get; set; }
    }
    class B 
    {
        [Import]
        public MyClass MyClass { get; set; }
    }

**Collections**

If an imported property (with `ImportAttribute`) is an `array[]` or an `IList<T>`, then `ResolveAll()` will 
be used to initialize this property as an appropriately typed collection.

**Circular Reference**

If you use property injection (as opposed to constructor injection) circular reference (i.e. circular dependencies) will work just fine.

**Custom Initialization**

Once an object and its properties have been initialization, if it implements `IRegistryDelegate`, its method 
`OnRegistryCreated()` will be called, for custom post creation initialization. All imported property will
have been set when this method is called.
