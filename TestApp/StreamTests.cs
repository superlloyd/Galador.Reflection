using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestApp
{
    public class StreamTests
    {
        [Fact]
        public void VariableLongStream()
        {
            var ms = new MemoryStream();

            var ul = 0x78ea0ul;
            int len = ms.WriteVUInt(ul);
            Assert.Equal(len, ms.Length);
            ms.Position = 0;
            var ul2 = ms.ReadVUInt();
            Assert.Equal(ul, ul2);
            Assert.True(ms.Length == 3);

            ul = 0xFFFFFFFFFFFFFFFFul;
            ms.Position = 0;
            len = ms.WriteVNUInt((ulong?)ul);
            Assert.Equal(len, ms.Length);
            ms.Position = 0;
            ul2 = ms.ReadVUInt();
            Assert.Equal(ul, ul2);
        }

        [Theory]
        [InlineData(0x3Ful)]
        [InlineData(0x40ul)]
        [InlineData(0x7Ful)]
        [InlineData(0x80ul)]
        [InlineData(0xFFFFFFFFFFFFFFFFul)]
        [InlineData(0ul)]
        [InlineData(null)]
        public void EdgeValueNUint(ulong? ul)
        {
            var ms = new MemoryStream();
            int len = ms.WriteVNUInt(ul);
            Assert.Equal(len, ms.Length);
            ms.Position = 0;
            var ul2 = ms.ReadVNUInt();
            Assert.Equal(ul, ul2);
        }

        [Theory]
        [InlineData(0x7Ful)]
        [InlineData(0x80ul)]
        [InlineData(0xFFFFFFFFFFFFFFFFul)]
        [InlineData(0)]
        public void EdgeValueUint(ulong ul)
        {
            var ms = new MemoryStream();
            int len = ms.WriteVUInt(ul);
            Assert.Equal(len, ms.Length);
            ms.Position = 0;
            var ul2 = ms.ReadVUInt();
            Assert.Equal(ul, ul2);
        }

        [Theory]
        [InlineData(0xFFFFL)]
        [InlineData(-0xFFFFL)]
        [InlineData(null)]
        public void SomeValuesNInt(long? l)
        {
            var ms = new MemoryStream();
            int len = ms.WriteVNInt(l);
            ms.Position = 0;
            var l2 = ms.ReadVNInt();
            Assert.Equal(l, l2);
        }

        [Theory]
        [InlineData(0xFFFF)]
        [InlineData(-0xFFFF)]
        [InlineData(0)]
        public void SomeValuesInt(long l)
        {
            var ms = new MemoryStream();
            int len = ms.WriteVInt(l);
            ms.Position = 0;
            var l2 = ms.ReadVInt();
            Assert.Equal(l, l2);
        }
    }
}
