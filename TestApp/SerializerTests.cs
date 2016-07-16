using Galador.Reflection.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestApp
{
    public class SerializationTests
    {
        [SerializationName("TEST-NAME", null)]
        public class Serial1
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public Serial2 Serial2 { get; set; }
        }
        [SerializationName("TEST-NAME", "TEST Assembly")]
        public class Serial2
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public Serial1 Serial1 { get; set; }
        }

        [Fact]
        public void CheckSerializationName()
        {
            var s1 = new Serial1
            {
                ID = 1,
                Name = "serial1",
                Serial2 = new Serial2
                {
                    ID = 2,
                    Name = "serial2",
                },
            };
            s1.Serial2.Serial1 = s1;

            // needed for attribute name to work during deserialization
            //KnownTypes.Register(typeof(Serial1), typeof(Serial2));

            var clone = Serializer.Clone(s1);
            Assert.Equal(s1.ID, clone.ID);
            Assert.Equal(s1.Name, clone.Name);
            Assert.Equal(s1.Serial2.ID, clone.Serial2.ID);
            Assert.Equal(s1.Serial2.Name, clone.Serial2.Name);
            Assert.Equal(s1.Serial2.Serial1.ID, clone.Serial2.Serial1.ID);

            var ms = new MemoryStream();
            var w = new ObjectWriter(new PrimitiveBinaryWriter(ms));
            w.Write(s1);

            var csharp = w.Context.GenerateCSharpCode("Generated");
            var t1 = w.Context.Objects.OfType<ReflectType>().First(x => x.Type == typeof(Serial1));
            var t2 = w.Context.Objects.OfType<ReflectType>().First(x => x.Type == typeof(Serial2));
            Assert.Equal(t1.TypeName, "TEST-NAME");
            Assert.Equal(t1.AssemblyName, null);
            Assert.Equal(t2.TypeName, "TEST-NAME");
            Assert.Equal(t2.AssemblyName, "TEST Assembly");
        }

        [Fact]
        public void ExoticTest()
        {
            dynamic d = new System.Dynamic.ExpandoObject();
            d.A = "B";
            d.Me = d;
            d.C = 42;
            var o = Serializer.Clone(d, false);
            Assert.Equal(d.A, o.A);
            Assert.Equal(d.C, o.C);
            Assert.Equal(o.Me, o);
        }

#if __NET__
        public void WinFormTest()
        {
            var d = new TestForm();
            var d2 = Serializer.Clone(d);
            d.Show();
            d2.Top += 10;
            d2.Left += 10;
            d2.Show();
            System.Windows.Forms.Application.Run();
        }
#endif

        class Annotation1
        {
            private Annotation1()
            {
                idd = 42;
            }
            public static Annotation1 Create() { return new Annotation1(); }
            [NotSerialized]
            public int ID { get; set; }
            [Serialized]
            private int idd;
            public void SetIDD(int id) { idd = id; }
            public int GetIDD() { return idd; }
        }
        [SerializationSettings(IncludeFields = true, IncludeProperties = false)]
        class Annotation2
        {
            public int ID1 { get; set; }
            public int ID2;
            private int ID3;

            public void SetID3(int id) { ID3 = id; }
            public int GetID3() { return ID3; }
        }
        [SerializationSettings(IncludePrivates = false, IncludeProperties = false)]
        class Annotation3
        {
            public int ID1 { get; set; }
            public int ID2;
            private int ID3;

            public void SetID3(int id) { ID3 = id; }
            public int GetID3() { return ID3; }
        }

        [Fact]
        public void CheckAnnotation()
        {
            var a = Annotation1.Create();
            a.ID = 23;
            a.SetIDD(18);
            var a2 = Serializer.Clone(a);
            Assert.Equal(0, a2.ID);
            Assert.Equal(18, a2.GetIDD());

            var b = new Annotation2
            {
                ID1 = 1,
                ID2 = 2,
            };
            b.SetID3(3);
            var b2 = Serializer.Clone(b);
            Assert.Equal(1, b2.ID1); // ahem property automatically saved with private backing field... not sure what to do about that....
            Assert.Equal(2, b2.ID2);
            Assert.Equal(3, b2.GetID3());

            var c = new Annotation3
            {
                ID1 = 1,
                ID2 = 2,
            };
            c.SetID3(3);
            var c2 = Serializer.Clone(c);
            Assert.Equal(0, c2.ID1);
            Assert.Equal(2, c2.ID2);
            Assert.Equal(0, c2.GetID3());
        }


        class Class1
        {
            public int ID1 { get; set; }
        }
        class Class2 : Class1
        {
            public int ID2 { get; set; }
        }

        [Fact]
        public void CheckSubclass()
        {
            var c = new Class2
            {
                ID1 = 1,
                ID2 = 2,
            };
            var c2 = Serializer.Clone(c);
            Assert.Equal(c.ID1, c2.ID1);
            Assert.Equal(c.ID2, c2.ID2);
        }

        struct Point2D
        {
            public Point2D(double x, double y)
            {
                this.x = x;
                this.y = y;
            }
            public double x, y;
            public override string ToString() { return $"{GetType().Name}({x}, {y})"; }
        };

        [Fact]
        public void CheckIsSmall()
        {
            var RAND = new Random();
            Func<Point2D> create = () => new Point2D(RAND.NextDouble(), RAND.NextDouble());
            int N = 100;
            var list = new List<Point2D>();
            for (int i = 0; i < N; i++)
                list.Add(create());

            var clone = Serializer.Clone(list);
            Assert.Equal(list.Count, clone.Count);
            for (int i = 0; i < list.Count; i++)
                Assert.Equal(list[i], clone[i]);

            var json = JsonConvert.SerializeObject(list);
            var meser = Serializer.ToSerializedString(list);
            Assert.True(meser.Length < json.Length - 4 * N);
        }

        [Fact]
        public void CheckWriteIsFastEnough()
        {
            var RAND = new Random();
            Func<Point2D> create = () => new Point2D(RAND.NextDouble(), RAND.NextDouble());
            int N = 100;
            var list = new List<Point2D>();
            for (int i = 0; i < N; i++)
                list.Add(create());

            int N2 = 500;

            var jDT = new Stopwatch();
            jDT.Start();
            for (int i = 0; i < N2; i++)
                JsonConvert.SerializeObject(list);
            jDT.Stop();

            var mDT = new Stopwatch();
            mDT.Start();
            for (int i = 0; i < N2; i++)
                Serializer.ToSerializedString(list);
            mDT.Stop();

            var bDT = new Stopwatch();
            bDT.Start();
            for (int i = 0; i < N2; i++)
            {
                var ms = new MemoryStream(256);
                Serializer.Serialize(list, ms);
            }
            bDT.Stop();

            Debug.WriteLine($"{jDT.ElapsedMilliseconds} {mDT.ElapsedMilliseconds} {bDT.ElapsedMilliseconds}");

            // REMARK: works **much** better (i.e. lower times) 
            // if the Serializer is compiled in RELEASE mode
            Assert.True(mDT.Elapsed.Ticks < jDT.Elapsed.Ticks);
        }

        [Fact]
        public void CheckReadIsFastEnough()
        {
            var RAND = new Random();
            Func<Point2D> create = () => new Point2D(RAND.NextDouble(), RAND.NextDouble());
            int N = 100;
            var list = new List<Point2D>();
            for (int i = 0; i < N; i++)
                list.Add(create());

            int N2 = 500;

            var json = JsonConvert.SerializeObject(list);
            var jDT = new Stopwatch();
            jDT.Start();
            for (int i = 0; i < N2; i++)
            {
                var o = JsonConvert.DeserializeObject(json, typeof(List<Point2D>));
            }
            jDT.Stop();

            var mtext = Serializer.ToSerializedString(list);
            var mDT = new Stopwatch();
            mDT.Start();
            for (int i = 0; i < N2; i++)
            {
                var o = Serializer.Deserialize(mtext);
            }
            mDT.Stop();

            var mem = new MemoryStream(256);
            Serializer.Serialize(list, mem);
            mem.Position = 0;
            Serializer.Deserialize(mem);
            System.Threading.Thread.Sleep(200);

            var bDT = new Stopwatch();
            bDT.Start();
            for (int i = 0; i < N2; i++)
            {
                mem.Position = 0;
                Serializer.Deserialize(mem);
            }
            bDT.Stop();

            Debug.WriteLine($"{jDT.ElapsedMilliseconds} {mDT.ElapsedMilliseconds} {bDT.ElapsedMilliseconds}");

            // REMARK: works **much** better (i.e. lower times) 
            // if the Serializer is compiled in RELEASE mode
            Assert.True(mDT.Elapsed.Ticks < jDT.Elapsed.Ticks);
        }

        class Generic01<T1, T2>
        {
            public List<T1> Elements;
            public List<T2> Elements2;
            public List<Tuple<T1, T2>> Elements3;
        }

        [Fact]
        public void CheckGeneric()
        {
            var o = new Generic01<int, string>
            {
                Elements = new List<int> { 1, 2 },
                Elements2 = new List<string> { "hello" },
                Elements3 = new List<Tuple<int, string>> { Tuple.Create(1, "haha") },
            };
            var o2 = Serializer.Clone(o, true);
            var o3 = Serializer.Clone(o, false);

            Assert.NotNull(o2.Elements);
            Assert.NotNull(o3.Elements);
            Assert.NotNull(o2.Elements2);
            Assert.NotNull(o3.Elements2);
            Assert.NotNull(o2.Elements3);
            Assert.NotNull(o3.Elements3);

            Assert.Equal(2, o2.Elements.Count);
            Assert.Equal(2, o3.Elements.Count);
            Assert.Equal(1, o2.Elements2.Count);
            Assert.Equal(1, o3.Elements2.Count);
            Assert.Equal(1, o2.Elements3.Count);
            Assert.Equal(1, o3.Elements3.Count);

            Assert.Equal(1, o2.Elements[0]);
            Assert.Equal(2, o2.Elements[1]);
            Assert.Equal("hello", o2.Elements2[0]);
            Assert.Equal(1, o2.Elements3[0].Item1);
            Assert.Equal("haha", o2.Elements3[0].Item2);

            Assert.Equal(1, o3.Elements[0]);
            Assert.Equal(2, o3.Elements[1]);
            Assert.Equal("hello", o3.Elements2[0]);
            Assert.Equal(1, o3.Elements3[0].Item1);
            Assert.Equal("haha", o3.Elements3[0].Item2);
        }

        public class BList : List<string>
        {
            public string Name { get; set; }
        }

        [Fact]
        public void CheckComplexClass2()
        {
            var b = new BList()
            {
                "one",
                "two",
                "threww",
            };
            b.Name = "haha";


            var c = Serializer.Clone(b);
            Assert.Equal(b.Count, c.Count);
            Assert.Equal(b[0], c[0]);
            Assert.Equal(b[1], c[1]);
            Assert.Equal(b[2], c[2]);
            Assert.Equal(b.Name, c.Name);
        }

        public class BigClass
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public List<object> Objects { get; } = new List<object>();
            public Dictionary<object, object> Values { get; } = new Dictionary<object, object>();
            public object Other { get; set; }
        }

        [Fact]
        public void CheckComplexClass()
        {
            var big = new BigClass()
            {
                ID = 42,
                Name = "Babakar",
                Objects = { "one", typeof(BigClass), 33, },
                Values = {
                    ["one"] = 1,
                },
                Other = new BigClass
                {
                    ID = 101,
                    Name = "what",
                },
            };
            big.Values["meself"] = big;
            big.Objects.Add(big);
            ((BigClass)big.Other).Other = big;

            var text = Serializer.ToSerializedString(big);
            var big2 = Serializer.Clone(big);
            Assert.Equal(big.ID, big2.ID);
            Assert.Equal(big.Name, big2.Name);
            Assert.Equal(big.Objects.Count, big2.Objects.Count);
            Assert.Equal("one", big2.Objects[0]);
            Assert.Equal(typeof(BigClass), big2.Objects[1]);
            Assert.Equal(33, big2.Objects[2]);
            Assert.Equal(big2, big2.Objects[3]);
            Assert.Equal(big.Values.Count, big2.Values.Count);
            Assert.Equal(1, big2.Values["one"]);
            Assert.Equal(big2, big2.Values["meself"]);
            Assert.IsType<BigClass>(big2.Other);
            var big3 = (BigClass)big2.Other;
            Assert.Equal(101, big3.ID);
            Assert.Equal("what", big3.Name);
            Assert.Equal(big2, big3.Other);
        }

        [Fact]
        public void CheckSimpleTypes()
        {
            Check(typeof(DateTime));
            Check("aloha");
            Check<string>(null);
            Check(DBNull.Value);
            Check(Guid.NewGuid());
            Check(true);
            Check(' ');
            Check((byte)23);
            Check(new TypeDescription(typeof(string[]))); // check TypeConverter
            Check((sbyte)23);
            Check((short)23);
            Check((ushort)23);
            Check(DayOfWeek.Thursday);
            Check((int)23);
            Check((int?)-1);
            Check((int?)null);
            Check((uint)23);
            Check((long)23);
            Check((ulong)23);
            Check(typeof(AA));
            Check(TimeSpan.FromMinutes(1.234)); // test surrogate
            Check(Tuple.Create(1, "oops")); // test surrogate
            Check(new double[,] { { 1, 2, 3 }, { 4, 5, 6 } }, (c1, c2) => {
                Assert.Equal(c1.Length, c2.Length);
                Assert.Equal(c1[0, 0], c2[0, 0]);
                Assert.Equal(c1[0, 1], c2[0, 1]);
                Assert.Equal(c1[0, 2], c2[0, 2]);
                Assert.Equal(c1[1, 0], c2[1, 0]);
                Assert.Equal(c1[1, 1], c2[1, 1]);
                Assert.Equal(c1[1, 2], c2[1, 2]);
            });
            Check(new int?[] { -3, null, 7 }, (c1, c2) => {
                Assert.Equal(c1.Length, c2.Length);
                Assert.Equal(c1[0], c2[0]);
                Assert.Equal(c1[1], c2[1]);
                Assert.Equal(c1[2], c2[2]);
            });
            Check(new int[][] { new int[] { 1, 2, 3 }, new int[] { 24, 5 } }, (c1, c2) => {
                Assert.Equal(c1[0][0], c2[0][0]);
                Assert.Equal(c1[0][1], c2[0][1]);
                Assert.Equal(c1[0][2], c2[0][2]);
                Assert.Equal(c1[1][0], c2[1][0]);
                Assert.Equal(c1[1][1], c2[1][1]);
            });
            Check(new byte[,] { { 1, 2, 3 }, { 4, 5, 6 } }, (c1, c2) => {
                Assert.Equal(c1.Length, c2.Length);
                Assert.Equal(c1[0, 0], c2[0, 0]);
                Assert.Equal(c1[0, 1], c2[0, 1]);
                Assert.Equal(c1[0, 2], c2[0, 2]);
                Assert.Equal(c1[1, 0], c2[1, 0]);
                Assert.Equal(c1[1, 1], c2[1, 1]);
                Assert.Equal(c1[1, 2], c2[1, 2]);
            });
            Check(new double[] { 1, 2, 3 }, (c1, c2) => {
                Assert.Equal(c1.Length, c2.Length);
                Assert.Equal(c1[0], c2[0]);
                Assert.Equal(c1[1], c2[1]);
                Assert.Equal(c1[2], c2[2]);
            });
            Check(new List<double> { 1, 2, 3 }, (c1, c2) => {
                Assert.Equal(c1.Count, c2.Count);
                Assert.Equal(c1[0], c2[0]);
                Assert.Equal(c1[1], c2[1]);
                Assert.Equal(c1[2], c2[2]);
            });
            Check(new List<string> { "one", "two", "three" }, (c1, c2) => {
                Assert.Equal(c1.Count, c2.Count);
                Assert.Equal(c1[0], c2[0]);
                Assert.Equal(c1[1], c2[1]);
                Assert.Equal(c1[2], c2[2]);
            });
            // test ISerializable
            Check(new Dictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2,
                ["three"] = 3,
            }, (c1, c2) => {
                Assert.Equal(c1.Count, c2.Count);
                Assert.Equal(c1["one"], c2["one"]);
                Assert.Equal(c1["two"], c2["two"]);
                Assert.Equal(c1["three"], c2["three"]);
            });
            Check((float)23);
            Check((double)23);
            Check((decimal)23);
            Check(typeof(List<>));
            Check<byte[]>(null, (b1, b2) => {
                Assert.Null(b1);
                Assert.Null(b2);
            });
            Check<byte[]>(new byte[] { 0, 1, 2 }, (b1, b2) => {
                Assert.Equal(b1.Length, b2.Length);
                for (int i = 0; i < b2.Length; i++) Assert.Equal(i, b2[i]);
            });
        }

        public class SimpleClass<T>
        {
            public T Val1 { get; set; }
            public T Val2 { get; set; }
            public object Val3 { get; set; }
            public object Val4 { get; set; }
            public T Val5;
            public object Val6;

            public void Set(T value)
            {
                Val1 = value;
                Val2 = value;
                Val3 = value;
                Val4 = value;
                Val5 = value;
                Val6 = value;
            }
            public void AssertClonningSuccess(T value, Action<T, T> assertTEqual = null)
            {
                Assert.True(Val3 == Val4);
                Assert.True(Val3 == Val6);
                Assert.Equal(Val1, Val2);
                Assert.Equal(Val1, Val3);
                Assert.Equal(Val1, Val5);
                if (assertTEqual != null) assertTEqual(value, Val5);
                else Assert.Equal(value, Val5);
                if (assertTEqual != null) assertTEqual(value, (T)Val3);
                else Assert.Equal(value, (T)Val3);
            }
        }

        void Check<T>(T value, Action<T, T> assertTEqual = null)
        {
            var wrap = new SimpleClass<T>();
            wrap.Set(value);

            var sb = new StringBuilder();
            var writer = new ObjectWriter(new PrimitiveTextWriter(new StringWriter(sb)));
            writer.Write(wrap);

            var text = sb.ToString();
            var reader = new ObjectReader(new PrimitiveTextReader(new StringReader(text)));
            var clone = reader.Read();

            Assert.IsType<SimpleClass<T>>(clone);
            var tclone = (SimpleClass<T>)clone;
            tclone.AssertClonningSuccess(value, assertTEqual);


            // this is a tad different, it uses BinaryWriter and an internal flag that write less TypeSurrogate info
            var clone2 = Serializer.Clone(wrap);
            clone2.AssertClonningSuccess(value, assertTEqual);
        }

        class AA
        {
        }
        class BB<T>
        {
            public class CC<U>
            {

            }
        }

        [Fact]
        public void CheckTypeRestored()
        {
            CheckType(typeof(int*));
            CheckType<int***[]>();
            CheckType<int***[,,]>();
            CheckType<int?[]>();
            CheckType<int?[][,,][,]>();
            CheckType<List<AA[,]>>();
            CheckType<AA[,,]>();
            CheckType(typeof(List<>));
            CheckType<string>();
            CheckType<int?>();
            CheckType<object>();
            CheckType<List<string>>();
            CheckType(typeof(KeyValuePair<,>));
            CheckType<SerializationTests>();
            CheckType<Dictionary<FactAttribute, AA>>();
            CheckType<AA>();
            CheckType<BB<int>>();
            CheckType<BB<AA>>();
            CheckType<Dictionary<Guid, Tuple<int, List<IDisposable>>>>();
            CheckType<BB<Dictionary<BB<int[]>[], List<BB<AA[,,]>>>>>();
            CheckType(typeof(BB<>));
            CheckType(typeof(BB<int>.CC<string>));
            CheckType(typeof(BB<>.CC<>));
        }
        void CheckType<T>() { CheckType(typeof(T)); }
        void CheckType(Type type)
        {
            var desc1 = new TypeDescription(type);
            var name = desc1.ToString();
            var desc2 = new TypeDescription(name);
            var parsed = desc2.Resolve();
            var lookup = Type.GetType(name);
            Assert.Equal(type, parsed);
            Assert.Equal(type, lookup);
            Assert.Equal(desc1, desc2);
        }

        [Fact]
        public void CheckReaderWriter()
        {
            var ms = new MemoryStream(256);
            CheckReaderWriter(
                () => new PrimitiveBinaryWriter(ms),
                () => {
                    ms.Position = 0;
                    return new PrimitiveBinaryReader(ms);
                });

            var sb = new StringBuilder(256);
            CheckReaderWriter(
                () => new PrimitiveTextWriter(new StringWriter(sb)),
                () => new PrimitiveTextReader(new StringReader(sb.ToString()))
                );

            var os = new List<object>(256);
            CheckReaderWriter(
                () => new TokenPrimitiveWriter(os),
                () => new TokenPrimitiveReader(os)
                );
        }
        void CheckReaderWriter(Func<IPrimitiveWriter> getW, Func<IPrimitiveReader> getR)
        {
            var w = getW();
            foreach (var rw in GetCheckers())
                rw.Item1(w);
            var r = getR();
            foreach (var rw in GetCheckers())
                rw.Item2(r);
        }
        IEnumerable<Tuple<Action<IPrimitiveWriter>, Action<IPrimitiveReader>>> GetCheckers()
        {
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write("hello")),
                (Action<IPrimitiveReader>)(r => Assert.Equal("hello", r.ReadString()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((string)null)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((string)null, r.ReadString()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write("hello\t\ner\r\ner")),
                (Action<IPrimitiveReader>)(r => Assert.Equal("hello\t\ner\r\ner", r.ReadString()))
            );
            byte[] buf = new byte[] { 2, 67, 23, 89 };
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write(buf)),
                (Action<IPrimitiveReader>)(r => {
                    var buf2 = r.ReadBytes();
                    Assert.Equal(buf.Length, buf2.Length);
                    for (int i = 0; i < buf.Length; i++)
                        Assert.Equal(buf[i], buf2[i]);
                })
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((byte[])null)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((byte[])null, r.ReadBytes()))
            );
            var guid1 = Guid.Parse("35fb0a0e-ee56-406b-bfa1-5f330ececce7");
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write(guid1)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(guid1, r.ReadGuid()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write(true)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(true, r.ReadBool()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write(false)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(false, r.ReadBool()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write('a')),
                (Action<IPrimitiveReader>)(r => Assert.Equal('a', r.ReadChar()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write('\n')),
                (Action<IPrimitiveReader>)(r => Assert.Equal('\n', r.ReadChar()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write('\u0066')),
                (Action<IPrimitiveReader>)(r => Assert.Equal('\u0066', r.ReadChar()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write('\u4566')),
                (Action<IPrimitiveReader>)(r => Assert.Equal('\u4566', r.ReadChar()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((byte)42)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((byte)42, r.ReadByte()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((sbyte)-23)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((sbyte)-23, r.ReadSByte()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((short)-2345)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((short)-2345, r.ReadInt16()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((ushort)2345)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ushort)2345, r.ReadUInt16()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((int)-2345)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((int)-2345, r.ReadInt32()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((uint)2345)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((uint)2345, r.ReadUInt32()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((long)long.MinValue + 2)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long)long.MinValue + 2, r.ReadInt64()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((ulong)ulong.MaxValue - 3)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ulong)ulong.MaxValue - 3, r.ReadUInt64()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((double)Math.PI)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((double)Math.PI, r.ReadDouble()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((double)Math.Pow(Math.E, 8))),
                (Action<IPrimitiveReader>)(r => Assert.Equal((double)Math.Pow(Math.E, 8), r.ReadDouble()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((float)Math.PI)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((float)Math.PI, r.ReadSingle()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((float)Math.Pow(Math.E, 8))),
                (Action<IPrimitiveReader>)(r => Assert.Equal((float)Math.Pow(Math.E, 8), r.ReadSingle()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write(4567.98075678m)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(4567.98075678m, r.ReadDecimal()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write("hello")),
                (Action<IPrimitiveReader>)(r => Assert.Equal("hello", r.ReadString()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.Write((string)null)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((string)null, r.ReadString()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(long.MaxValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(long.MaxValue, r.ReadVInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(long.MinValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(long.MinValue, r.ReadVInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(0L)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(0L, r.ReadVInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(42L)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(42L, r.ReadVInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(ulong.MaxValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(ulong.MaxValue, r.ReadVUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(33UL)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(33UL, r.ReadVUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt(0UL)),
                (Action<IPrimitiveReader>)(r => Assert.Equal(0UL, r.ReadVUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((long?)null)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long?)null, r.ReadVNInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((long?)long.MaxValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long?)long.MaxValue, r.ReadVNInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((long?)long.MinValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long?)long.MinValue, r.ReadVNInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((long?)0L)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long?)0L, r.ReadVNInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((long?)42L)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((long?)42L, r.ReadVNInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((ulong?)null)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ulong?)null, r.ReadVNUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((ulong?)ulong.MaxValue)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ulong?)ulong.MaxValue, r.ReadVNUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((ulong?)33UL)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ulong?)33UL, r.ReadVNUInt()))
            );
            yield return Tuple.Create(
                (Action<IPrimitiveWriter>)(w => w.WriteVInt((ulong?)0UL)),
                (Action<IPrimitiveReader>)(r => Assert.Equal((ulong?)0UL, r.ReadVNUInt()))
            );
        }
    }
}
