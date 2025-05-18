using Galador.Reflection.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp;

public class DocumentTest
{
    // had an error in my code that caused the serialization to fail
    // simple repro below (should not fail, of course!)
    [Fact]
    public void TestMapDocSerialization()
    {
        var memt = new MemTraceListener();
        Trace.Listeners.Add(memt);

        var map = new TestMap();
        map.Layers.Add(new TestVectorLayer() { Name = "Layer 1" });
        map.Layers.Add(new TestVectorLayer() { Name = "Layer 2" });

        var map2 = map.Clone() as TestMap;
        Assert.Equal(map.Layers.Count, map2!.Layers.Count);

        var deserialized = Serializer.Clone(map2);
        // this fails, but the unexpected datat structure caused unexpeceted deserialization
        // make sure there is an Asset warning
        Assert.Contains(memt.Messages, x => x.Contains("Object.Read()"));
    }

    class MemTraceListener : TraceListener
    {
        public List<string> Messages { get; } = new List<string>();
        StringBuilder currentLine = new StringBuilder(); 

        public override void Write(string? message)
        {
            currentLine.Append(message);
        }
        public override void WriteLine(string? message)
        {
            if (currentLine.Length > 0)
            {
                currentLine.Append(message);
                Messages.Add(currentLine.ToString());
                currentLine.Clear();
            }
            else if (!string.IsNullOrEmpty(message))
            {
                Messages.Add(message);
            }
        }
    }

    public interface IParented
    {
        object? Parent { get; set; }
    }
    [SerializationSettings(false, IncludeFields = true)]
    public class TestList<T> : ICollection<T>, IParented, ICloneable
    {
        List<T> _innerList = new List<T>();

        public TestList(object? parent)
        {
            Parent = parent;
        }
        public object? Parent { get; set; }

        public int Count => _innerList.Count;
        public bool IsReadOnly => false;
        public void Add(T item)
        {
            if (item is IParented p)
                p.Parent = this;
            _innerList.Add(item);
        }
        public void AddRange(params IEnumerable<T> item)
        {
            foreach (var i in item)
            {
                if (i is IParented p)
                    p.Parent = this;
            }
            _innerList.AddRange(item);
        }
        public void Clear() => _innerList.Clear();
        public bool Contains(T item) => _innerList.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _innerList.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => _innerList.GetEnumerator();
        public bool Remove(T item) => _innerList.Remove(item);
        IEnumerator IEnumerable.GetEnumerator() => _innerList.GetEnumerator();

        public object Clone()
        {
            var l = (TestList<T>)MemberwiseClone();
            l._innerList = _innerList.Select(x =>
            {
                if (x is ICloneable c && x is IParented)
                {
                    var x2 = (T)c.Clone();
                    ((IParented)x2).Parent = l;
                    return x2;
                }
                else
                {
                    return x;
                }
            }).ToList();
            return l;
        }
    }

    [SerializationSettings(false, IncludeFields = true)]
    public class TestMap : ICloneable
    {
        public TestMap()
        {
            mLayers = new TestList<TestVectorLayer>(this);
        }

        public TestList<TestVectorLayer> Layers => mLayers;
        TestList<TestVectorLayer> mLayers;

        public object Clone()
        {
            var m = (TestMap)MemberwiseClone();
            m.mLayers = (TestList<TestVectorLayer>)mLayers.Clone();
            m.mLayers.Parent = m;
            return m;
        }
    }
    [SerializationSettings(false, IncludeFields = true)]
    public class TestVectorLayer : ICloneable, IParented
    {
        public TestVectorLayer()
        {
            mItems = new TestList<TestLayerItem>(this);
        }
        public object? Parent { get; set; }

        [NotSerialized]
        public TestList<TestLayerItem> Items => mItems;
        [Serialized]
        TestList<TestLayerItem> mItems;

        public string? Name { get; set; }

        public object Clone()
        {
            var l = (TestVectorLayer)MemberwiseClone();
            l.mItems = new TestList<TestLayerItem>(this); // this was a bug in my code, that cause very strange deserialization!
            //l.mItems = new DocumentList<TestLayerItem>(l);
            l.mItems.AddRange(mItems.Select(x => (TestLayerItem)x.Clone()));
            return l;
        }
    }
    public class TestLayerItem : ICloneable, IParented
    {
        public TestLayerItem()
        {
        }
        public TestLayerItem(string name)
        {
            Name = name;
        }
        public object? Parent 
        {
            get => field;
            set => value = field;
        }


        public string? Name { get; set; }
        public object Clone()
        {
            var l = (TestVectorLayer)MemberwiseClone();
            l.Parent = null;
            return l;
        }
    }
}
