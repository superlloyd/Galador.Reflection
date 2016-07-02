using Galador.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestApp
{
    public class RegistryTests
    {
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
            Assert.Equal(o.Count, 1);
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
            r.RegisterAssemblies(GetType().Assembly);

            var o = r.ResolveAll<IBar>().ToList();
            Assert.Equal(o.Count, 1);
        }
    }
}
