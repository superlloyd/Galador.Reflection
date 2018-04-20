using Galador.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestApp
{
    public class RegistryTests
    {
        public class Disp1 : IDisposable { void IDisposable.Dispose() { } }
        public class Disp2 : IDisposable { void IDisposable.Dispose() { } }
        public class Disp3 : IDisposable { void IDisposable.Dispose() { } }

        class HasDispose
        {
            [Import]
            public IDisposable[] Disposables { get; set; }
            [Import]
            public List<IDisposable> DisposableList { get; } = new List<IDisposable>();
        }
        
        [Fact]
        public void CheckListInjection()
        {
            var reg = new Registry();
            reg.Register(typeof(Disp1), typeof(Disp2), typeof(Disp3));
            var h = reg.Resolve<HasDispose>();
            Assert.Equal(3, h.Disposables.Length);
            Assert.Equal(3, h.DisposableList.Count);
            for (int i = 0; i < 3; i++)
                Assert.Equal(h.Disposables[i], h.DisposableList[i]);
        }

        interface IService1 { }
        [Export]
        class ServiceImpl1 : IService1
        {
            [Import]
            public Service2 Service2 { get; set; }
        }
        class Service2
        {
            [Import]
            public ServiceImpl1 Service1 { get; set; }
            [Import]
            public Service3 Service3 { get; set; }
        }
        class Service3
        {
            public Service3(IService1 svc)
            {
                Service1 = svc;
            }
            public IService1 Service1 { get; set; }
        }

        [Fact]
        public void TestInjection()
        {
            var reg = new Registry();
            reg.Register<ServiceImpl1>();
            var s2 = reg.Resolve<Service2>();
            Assert.NotNull(s2.Service1);
            Assert.NotNull(s2.Service3);
            Assert.NotNull(s2.Service3.Service1);
            Assert.Equal(s2.Service3.Service1, s2.Service1);
            Assert.Equal(s2.Service1.Service2, s2);
        }

        interface IFoo
        {
            Too MyToo { get; }
        }
        interface IBar
        {
        }
        [Export]
        class Bar1 : IBar { }
        class Bar2 : IBar { }
        class FooImpl : IFoo
        {
            public FooImpl(Too tt)
            {

            }
            [ImportAttribute]
            public Too MyToo { get; set; }
            [ImportAttribute]
            public IBar[] Bars { get; set; }
        }
        class Too
        {

        }

        [Fact]
        public void Create1()
        {
            var t0 = new FooImpl(null);

            var r = new Registry();
            r.Register<FooImpl>();
            r.Register<Bar1>();
            r.Register<Bar2>();

            var o = r.ResolveAll<IFoo>().ToList();
            Assert.Equal(1, o.Count);
            Assert.NotNull(o[0]);
            var oo = o[0];
            Assert.NotNull(oo.MyToo);
            Assert.True(oo is FooImpl);
            var fi = (FooImpl)oo;
            Assert.Equal(2, fi.Bars.Length);
            Assert.True(fi.Bars.First(x => x is Bar1) != null);
            Assert.True(fi.Bars.First(x => x is Bar2) != null);
        }

        [Fact]
        public void Create2()
        {
            var t0 = new FooImpl(null);

            var r = new Registry();
            r.RegisterAssemblies<ExportAttribute>();

            var o = r.ResolveAll<IBar>().ToList();
            Assert.Equal(1, o.Count);
        }
    }
}
