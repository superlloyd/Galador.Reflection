## Galador.Reflection
[API Documentation](Galador.Reflection.chm?raw=true)

Here are a collection of reflection based multiplatform .NET utilities. The project is a VS2015 Community solution.

Using bait and switch trick there is only 1 DLL for each supported platforms, however the PCL DLL is used only at compile time and **should** not be used at runtime.
Use the **iOS**, **Android**, **.NET4.5** or **.NET Core** DLL instead.

Look in the test apps for additional usage sample.

### Getting Started

    Install-Package Galador.Reflection

The package for  *PCL (iOS, Android, .NET)*, **iOS**, **Android**, **.NET4.5** has been published.

**REMARK** *.NET Core* support for now is only in source form. I could not properly name
the project and share the .NET Core source instead of duplicating them. 
However all test currently run successfully on .NET Core.

**REMARK** The source here is contained in a VS 2015 project. It is also required since I use some C# 6 and .NET 4.5 features.
One can download VS2015 for free [here](https://www.visualstudio.com/products/visual-studio-community-vs). 

**REMARK** The minimum .NET version supported is .NET4.5. There are so many little things which don't amount to much separately,
but all together will make porting it to earlier version of .NET just a pain I am not interested in.


### Serializer
[Details](serializer.md)

A class which helps implement `File > Save` with very little setup. 
By design it ignores pointers, `IntPtr`, `Delegate`. Also it is designed to work with object that can fully be describe by their public fields and/or properties and, optionally by `IList` or `IDictionary` interfaces (generic or not).
And it will NOT restore private field / property, unless explicitly told to, with some attribute annotation on the class itself.
Very much like JSON value property / field are matched by name when deserializing. 
But unlike JSON default format this also store object as reference and save type information.

To serialize an object one does:

    var o = ....
    using var mem = new MemoryStream();
    using var writer = new PrimitiveBinaryWriter(mem);
    Serializer.Serialize(o, writer);

To deserialize an object one does

    using var mem = OpenFile();
    using var reader = new PrimitiveBinaryReader(mem);
    var o = Serializer.Deserialize(reader);

To serialize opaque type, such as a `Bitmap` one must create a surrogate class

    class BitmapSurrogate : ISurrogate<Bitmap>
    {
       public void Initialize(Bitmap bmp) { ... }
       public Bitmap Instantiate() { ... }

       // data now exposed as public supported property, hence can be serialized
       public byte[] Data { get; set; }
    }


Finally if provided with a stream created by a third party with this Serializer, one can use `ObjectContext.GenerateCSharpCode()` 
or `Serializer.GenerateCSharpCode()` to generate a class hierarchy that can be used to deserialize the stream.


### Registry
[Details](registry.md)

An IoC container, `IServiceProvider`, MEF clone all in one.

First create an instance of a registry `var registry = new Registry()`

Service class are then registered with `registry.Register<T>()` or all of those marked with `ExportAttribute` via `registry.RegisterAssemblies(...)`.
Remark class (not interface) class should be registered. They could then be access though **any** type or interface they implement.

After service registration it can be use as a replacement for the `Activator` class, 
it will also do constructor injection (using the constructor with the most parameters) 
and property injection, when a property is marked with `ImportAttribute`. 
The injection will use all the interfaces registered previously, or create instances on demand recursively.

Finally to solve mutually dependent object implement `IRegistryDelegate` such as

    class A : IRegistryDelegate
    {
        [Import]
        public B B { get; set; }

        void IRegistryDelegate.OnRegistryCreated() {
            B.DoSomething();
        }
    }

    class B
    {
        [Import]
        public A A{ set; set; }

        public void DoSomething() { ... }
    }
    var registry = new Registry();
    var a = registry.Resolve<A>();



### PropertyPath
An handy little utility to observe property path, help synchronize POCO class as much as  WPF does with binding.
It also register weak event so one should keep a handler to it to keep it alive.
It watches `INotifyPropertyChange` interface for change, so it is better used with MVVM data models.

Here is a simple use case, of registering an event when a property changes:

    void SetModel(Model m)
    {
        modelWatcher = PropertyPath.Watch(m, x => x.Location.City, city => {
            this.City = city;
        });
    }
    PropertyPath modelWatcher;


### FastType
Unfortunately, even in 2016! .NET reflection has pitiful performance. `FastType` provide
quick (default) constructor and member getter and setter on platform that support 
[System.Emit](https://docs.microsoft.com/en-us/dotnet/core/api/system.reflection.emit),
(i.e. full .NET framework and .NET core, but not Android/iOS) falling back on normal reflection otherwise.

Example:

    var FT = FastType.GetType(typeof(List<object>));
    var list = FT.TryConstruct();
    FT.DeclaredMembers["Capacity"].SetValue(list, 42);

    // same as
    var list = new List<object>();
    list.Capacity = 42;


### TraceKeys

This is one of my utility which only happen to be here for convenience.
It is a thin multiplatform wrapper around [System.Diagnostics.Trace](https://docs.microsoft.com/en-us/dotnet/core/api/system.diagnostics.trace#System_Diagnostics_Trace)
with the added benefit that whole traces can be turned on or off at once *easily* (with `TraceKey.IsEnabled`).
Hence all TraceListeners apply to it (when the key is enabled).

One get a `TraceKey` with `TraceKeys.Traces[name]` or use an already defined one such
as `TraceKeys.Application`. Then call any of its method to log something.
All methods but `TraceKey.Write()` and `TraceKey.WriteLine()` will start the output line with `TraceKey.Header`
which by default contains the trace name, current date time and thread id.

By default all `TraceKey` are disabled (except `TraceKeys.Application`). On the desktop (full .NET framework)
they will automatically pick up the `AppSettings` values for `"TraceKeys." + key.Name`
(text value must be in `"true" "false" "0" "1" "on" "off" "enable" "disable"`).

Examples:

    // enable Serializer logging
    // with AppSettings
    <add key="TraceKeys.Serialization" value="true"/>
    // with code
    TraceKeys.Serialization.IsEnabled = true;

    // use your own key
    public static readonly LogData = TraceKeys.Traces[$"{nameof(Model)} {nameof(LogData)}"];
    // or
    public static readonly LogData = TraceKeys.Traces.GetTrace("Model LogData", t => t.IsEnabled = true);

    // enable it with AppSettings
    <add key="TraceKeys.Model LogData" value="on"/>

    // use it
    LogData.WriteLine(data);
    LogData.Information("all good")
    LogData.Error(exception);

    // enable all traces with code
    foreach (var trace in TraceKeys.Traces)
        trace.IsEnabled = true;