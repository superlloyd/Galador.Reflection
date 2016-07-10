## Galador.Reflection

Here are a collection of reflection based multiplatform utilities.
Using bait and switch trick there is only 1 DLL for each supported platforms. So far iOS, Android, Desktop .NET. 
.NET Core support will come later...
UWP will NOT...

Not much documentation (for now), look in the **TestApp** project for the Unit test class for an idea on where to start.

Most interesting classed exposed:

### Serializer
[Details](serializer.md)

A class which helps implement `File > Save` with very little setup. 
By design it ignores pointers, `IntPtr`, `Delegate`. Also it is designed to work with object that can fully be describe by their public fields and/or properties and, optionally by `IList` or `IDictionary` interfaces (generic or not).
And it will NOT restore private field / property, unless explicitly told to, with some attribute annotation on the class itself.
Very much like JSON value property / field are matched by name when deserializing. 
But unlike JSON default format this also store object as reference and save type information.

To serialize an object one does:

    var o = ....
    var mem = new MemoryStream();
    Serializer.Serialize(o, mem);

To deserialize an object one does

    var mem = OpenFile();
    var o = Serializer.Deserialize(mem);

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