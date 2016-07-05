Latest version at:

https://github.com/superlloyd/Galador.Reflection

Here I would describe the motivation, internal and use cases of my Serializer library found at:
https://github.com/superlloyd/Galador.Reflection

## Introduction

I am working on some sort of desktop graph editor. I need to save my data to disk and load those files. So this serializer is written with persistence in mind.
Also it should be able to read document made by different version of the application, hence it is tolerant to type and version mismatch.

I rejected the build-in serialization mechanism in .NET (that is DataContractSerializer, XmlSerializer, BinaryFormatter) as unsatisfactory. 
BinaryFormatters have versioning issues. And DataContact and XML serializer do not work very well when the type to deserialize is not know in advance and they are prone to serialization bug, if one forget a DataMember attribute.
On the other hand JsonSerializer is very easy and intuitive to use (just make your property / field public) but it doesn't handle subclass very well. And, worst of all, it is way to verbose.

Enter my serializer (`Galador.Reflection.Serialization.Serializer`), it supports both binary and text format. Text format is much more compact than Json (human readable but not human friendly). It is also faster than Newtonsoft.Json.
Any type that has been serialized is completely described in the stream, along with the data. One can generate needed type hierarchy to read the stream from the stream itself. And when deserializing type and property are matched ny name when possible, or just ignored.
It should support most classes whose state is fully described by their public field / property, ISerializable, IList and IDictionary interface. Except `IntPtr`, pointers and Delegate objects.

When deserializing it will seldom throw an error. If some property do no match (from the source stream or on the target) they will just silently be ignored. 
This is by design, despite being internally strongly typed, the serializer behave as conveniently as the Json deserialization process.
On the other hand data could be unknowingly missed.


## Common Use Cases

How does one save an object? as simply as that

    public void Save(object o, Stream s)
    {
        Serializer.Serialize(o, s);
    }

Note that the serializer does **NOT** `Stream.Seek()` and can use any writable stream for serialization. And any readable stream for deserialization.
Such as `GZipStream` or network stream or whatever.

And the reading is just as easy:

    public void Read<T>(Stream s)
    {
        var o = Serializer.Deserialize(s);
        return o as T;
    }
    
The only, optional, setup is to register your assemblies with 

     KnownObjects.Register(typeof(MyType).Assemby)

It will enable a few advanced features like `ISurrogate<T>` (to serialize opaque type such as `Bitmap`) and naming type (with `SerializationNameAttribute`).

Generating a C# code for a third party type hierarchy is done with 

    public string GetCSharpCode(Stream s, string @namespace)
    {
        var or = new ObjectReader(new PrimitiveBinaryReader(s));
        or.Read();
        return or.Context.GenerateCSharpCode(@namespace);
    }

## Implementation

Since reading is just the reverse process of writing, I will only describe the writing process here. 

`ObjectWriter` doesn't write to a stream of byte directly but to an `IPrimitiveWriter` interface.

`IPrimitiveWriter` can write all primitive types, 
i.e. `bool, char, byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, Guid, byte[], string`.
It also have 4 `WriteVInt()` overload to write `long, long?, ulong, ulong?` in the most compact way much like UTF8 encoding.
i.e. any number between 0 to 127 will take 1 byte, then 128 to 8000 will take 2 bytes, and so on. These special `WriteVInt()` methods
will be used for object ID, array length and such like.

`ReflectType` is the internal description of type and contains all information needed about it, whether it's a primitive or not,
the generic definition and argument, array rank, type and assembly name, member (property and field), etc...
One can get a `ReflectType` with `ReflectType.GetType()`

`ObjectWriter` implementation is as follow:

    public void Write(object o)
    {
        writer.Write(VersionNumber)
        Write(ReflectType.Object, o);
    }

    internal void Write(ReflectType expected, object o)
    {
        // 1. if object is in Context, write ID then **return**
        //    else write (newly acquireded ID) ID

        // 2. if expected is NOT final, i.e. is a non-sealed class (ex: this step is omitted for struct)
        if (expected.CanBeSubclassed) Write(ReflectType.Type, ReflectType.GetType(o)); // recursion

        // 3. write object data
    }

i.e. graphically data would be as follow
    
    Write(Type t, object o): | Version | GetID(Object) | Write(ReflectType.Type), ReflectType.GetType(o)) | WriteData(o) 

*Step 1*, ID, is here to ensure that each object is written only once. Further write will only write the ID.

*Step 2*, write the type meta data, so that the data can be deserialized. It only done when the type is not known for sure. 
i.e if the expected type is sealed or is a value type, there is no need to write type information.
Further if the type has already be written just its ID will be written.

*Step 3* `WriteData()` has many possible cases. Even an abbreviated version would be too long. Suffice to say that it checks:  
- If the type is either `Type` or `ReflectType`
- If the type has `TypeConverter`
- If the type has an `ISurrogate<T>`
- If the type is an array (any rank)
- If the type is `Nullable<>`
- If the type is an Enum
- If the type is a primitive type (written directly to the `IPrimitiveWriter`)
- In all other case it will be written as a simple object

    // here is the default write object for simple object
    void WriteObject(ReflectType type, object o)
    {
        foreach(Member m in type.RuntimeMembers())
            Write(m.Type, m.GetValue(o)); // recursion

        switch (type.CollectionType)
        {
            case ReflectCollectionType.None: // Nothing!
            case ReflectCollectionType.Ilist: // write IList
                {
                    var l = (IList)o;
                    writer.WriteVInt(l.Count);
                    for (int i=0; i < l.Count; i++)
                        Write(ReflectType.Object, l[i]); // recursion
                }
            case ReflectCollectionType.ICollectionT: // write IList<T>
            case ReflectCollectionType.IDictionary: // write IDictionary
            case ReflectCollectionType.IDictionaryKV: // write IDictionary<K, V>
        }
    }
    
That is in essence the whole algorithm. All the use case made it a bit tedious, yet relatively simple.
`ObjectWriter` is about 400 lines, `ObjectReader` is about 500 lines and `ReflectType` is about 1000 lines.

## Example Output

 To show how compact is the result I will take this simple example of an array of 5 points.

    struct Point2D
    {
        public Point2D(double x, double y) { this.x = x; this.y = y; }
        public double x, y;
    }
    public void Create()
    {
        var RAND = new Random();
        Func<Point2D> create = () => new Point2D(RAND.NextDouble(), RAND.NextDouble());
        int N = 5;
        var list = new List<Point2D>();
        for (int i = 0; i < N; i++)
            list.Add(create());

        var json = JsonConvert.SerializeObject(list);
        var meser = Serializer.ToSerializedString(list);
        var w = new ObjectWriter(new PrimitiveTextWriter(new StringWriter()));
        w.Write(list);
        var csharp = w.Context.GenerateCSharpCode("Generated");
    }



Json output is as follow, no type information and redundant "x", "y" "{}" "[]" repeated for every values:

    [{"x":0.56640757460445523,"y":0.66867056333863673},{"x":0.94807661927681264,"y":0.15675181483698628},
    {"x":0.41150656361668675,"y":0.74362293618899911},{"x":0.0069035766678413272,"y":0.8712547942396508},
    {"x":0.5437086026853456,"y":0.6287862400658365}]

By contrast my serializer output start with a bulky MetaData Type header, followed by compact data part, i.e. only Xs and Ys:

    2 7 8 805569025 9 806093313 10 "System.Collections.Generic.List`1" 0 1 2 11 "Capacity" 12 16777227 13 "Count" 
    12 14 17825792 0 1 15 553648129 16 "Galador.Core.Tests.SerializationTests+Point2D" 17 "Galador.Core.Tests" 2 18 
    "x" 19 16777232 20 "y" 19 8 5 5 
    0.56640757460445523 0.66867056333863673 0.94807661927681264 0.15675181483698628 0.41150656361668675 0.74362293618899911 
    0.0069035766678413272 0.8712547942396508 0.5437086026853456 0.6287862400658365 

Also if I lose that code I can generate a reader class, here is the generated C#

    // <auto-generated>
    //     This code was generated by a tool.
    //     But might require manual tweaking.
    // </auto-generated>

    using System.ComponentModel;
    using System.Collections;
    using System.Collections.Generic;

    namespace Generated {

	    [SerializationNameAttribute("Galador.Core.Tests.SerializationTests+Point2D", "Galador.Core.Tests")]
	    public struct Type15
	    {
		    public Type15() { }
		    public double x { get; set; }
		    public double y { get; set; }
	    }
    }

## Advanced Usage

### Serializer
The `Serializer` class is just a convenient wrapped around the `ObjectReader` `ObjectWriter`, and `IPrimitiveWriter` classes.

One must first create an `IPrimitiveReader` / `IPrimitiveWriter` implementation

    public class PrimitiveTextWriter : IPrimitiveWriter
    {
        public PrimitiveTextWriter(TextWriter writer);
    }
    public class PrimitiveTextReader : IPrimitiveReader
    {
        public PrimitiveTextReader(TextReader writer);
    }

    public class PrimitiveBinaryWriter : IPrimitiveWriter
    {
        public PrimitiveBinaryWriter(Stream stream);
    }
    public class PrimitiveBinaryReader : IPrimitiveReader
    {
        public PrimitiveBinaryReader(Stream stream);
    }


Then create a writer (for serialization)

    public class ObjectWriter : IDisposable
    {
        public ObjectWriter(IPrimitiveWriter writer);
        public ObjectContext Context { get; private set; }

        public void Write(object o)
    }
call `writer.Write(o)` to serialize object `o`.

Or a Reader, for deserialization

    public class ObjectReader : IDisposable
    {
        public ObjectReader(IPrimitiveReader writer);
        public ObjectReader Context { get; private set; }

        public object Read()
    }
call `var o = reader.Read()` to get the next object in the stream.

### Handling unknown data

when calling `reader.Read()` one can get a `Galador.Reflection.Serializer.Missing` if the target type is absent or has not been found.
One can inspect the `reader.Context.Objects` for all `ReflectType` loaded to get an idea of what type are missing.

One can also generate appropriate C# code, after a call to `Read()` by calling `reader.Context.GenerateCSharpCode()`

### Serialization Attributes

When deserializing type are matched on `FullName` (with namespace) and assembly name (no version or hash, just the name).
Hence serialization will break if the assembly change name of or the type change name.

One can prevent that by giving an arbitrary (unique) name to the type with `SerializationNameAttribute` as in

    // these names can be any string!
    [SerializationName("unique pseudo class name", "pseudo assembly name)] 
    public MyClass 
    {
    }
**Remark** One **must** register the assemblies with named type with `KnownTypes.Register()` for named type to function.


Maybe for some type one would like to save all field (whether public or private) and no property? or some other settings?
One can control which field / property will be seen with those attribute

First on the class, for general directive:

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class SerializationSettingsAttribute : Attribute
    {
        public SerializationSettingsAttribute();
        public SerializationSettingsAttribute(bool @default);

        public bool IncludePublicProperties { get; set; } = true;
        public bool IncludePrivateProperties { get; set; } = false;
        public bool IncludePublicFields { get; set; } = true;
        public bool IncludePrivateFields { get; set; } = false;

        public bool IncludePublics
        {
            get { ... }
            set { ... }
        }
        public bool IncludePrivates
        {
            get { ... }
            set { ... }
        }
        public bool IncludeProperties
        {
            get { ... }
            set { ... }
        }
        public bool IncludeFields
        {
            get { ... }
            set { ... }
        }
    }
 
Then on a particular field and / or property

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SerializedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class NotSerializedAttribute : Attribute
    {
    }

### Extending serialization
There might some type that you want to save that do not support out of the box serialization, such as `Bitmap` or `Icon`? 
    One must then implement `ISurrogate<T>`

    public interface ISurrogate<T>
    {
        void Initialize(T value);
        T Instantiate();
    }
And one **must** register with known type all the assembly that contains ISurrogates with `KnownTypes.Register()`

Here is 2 quick surrogate example

    public class ApplicationSingletonSurrogate : ISurrogate<Application>
    {
        void Initialize(Application value) { }
        Application Instantiate() { return Application.Current; }
    }

    public class BitmapSurrogate : ISurrogate<Bitmap>
    {
        void Initialize(Bitmap value) 
        { 
            var ms = new MemoryStream();
            value.Save(ms, ImageFormat.Png);
            Data = ms.ToArray();
        }
        Bitmap Instantiate() { return new Bitmap(new MemoryStream(Data)); }

        public byte[] Data {get; set; }
    }

### Post deserialization activity
There might be some code you want to run once an object has been fully deserialized, 
    like wiring some events (since event are not restored by the deserializer)
    or updating some private variable.

Implement `IDeserialized` for that purpose and your object will be called once deserialized.

    public interface IDeserialized
    {
        void Deserialized();
    }
