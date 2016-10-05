using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using Galador.Reflection.Utils;
using System.Runtime.InteropServices;

namespace Galador.Reflection.IO
{
    // REMARK BitConverter follow Indianness, serializer should not, hopefully this will fix it
    [StructLayout(LayoutKind.Explicit)]
    struct Union8
    {
        [FieldOffset(0)]
        public char Char;
        [FieldOffset(0)]
        public long Int64;
        [FieldOffset(0)]
        public ulong UInt64;
        [FieldOffset(0)]
        public int Int32;
        [FieldOffset(0)]
        public uint UInt32;
        [FieldOffset(0)]
        public short Int16;
        [FieldOffset(0)]
        public ushort UInt16;
        [FieldOffset(0)]
        public float Single;
        [FieldOffset(0)]
        public double Double;

        [FieldOffset(0)]
        public byte Byte0;
        [FieldOffset(1)]
        public byte Byte1;
        [FieldOffset(2)]
        public byte Byte2;
        [FieldOffset(3)]
        public byte Byte3;
        [FieldOffset(4)]
        public byte Byte4;
        [FieldOffset(5)]
        public byte Byte5;
        [FieldOffset(6)]
        public byte Byte6;
        [FieldOffset(7)]
        public byte Byte7;
    }

    /// <summary>
    /// An <see cref="IPrimitiveWriter"/> writing to a <see cref="Stream"/>.
    /// </summary>
    public class PrimitiveBinaryWriter : IPrimitiveWriter
    {
#pragma warning disable 1591 // XML Comments
        Union8 union;
        Stream Stream;

        public PrimitiveBinaryWriter(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            Stream = stream;
        }
        public void Dispose() { Stream.Dispose(); }

        public void Write(string value)
        {
            byte[] buf = null;
            if (value != null)
            {
                if (value.Length == 0)
                    buf = Empty<byte>.Array;
                else
                    buf = Encoding.UTF8.GetBytes(value);
            }
            this.Write(buf);
        }

        public void Write(byte[] value)
        {
            var len = value == null ? (ulong?)null : (ulong)value.Length;
            Stream.WriteVNUInt(len);
            if (value != null && value.Length > 0)
            {
                Stream.Write(value, 0, value.Length);
            }
        }

        public void Write(Guid value)
        {
            var buf = value.ToByteArray();
            Stream.Write(buf, 0, buf.Length);
        }

        public void Write(bool value)
        {
            Stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void Write(char value)
        {
            union.Char = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
        }

        public void Write(byte value)
        {
            Stream.WriteByte(value);
        }

        public void Write(sbyte value)
        {
            Stream.WriteByte(unchecked((byte)value));
        }

        public void Write(short value)
        {
            union.Int16 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
        }

        public void Write(ushort value)
        {
            union.UInt16 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
        }

        public void Write(int value)
        {
            union.Int32 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
        }

        public void Write(uint value)
        {
            union.UInt32 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
        }

        public void Write(long value)
        {
            union.Int64 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
            Stream.WriteByte(union.Byte4);
            Stream.WriteByte(union.Byte5);
            Stream.WriteByte(union.Byte6);
            Stream.WriteByte(union.Byte7);
        }

        public void Write(ulong value)
        {
            union.UInt64 = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
            Stream.WriteByte(union.Byte4);
            Stream.WriteByte(union.Byte5);
            Stream.WriteByte(union.Byte6);
            Stream.WriteByte(union.Byte7);
        }

        public void Write(float value)
        {
            union.Single = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
        }

        public void Write(double value)
        {
            union.Double = value;
            Stream.WriteByte(union.Byte0);
            Stream.WriteByte(union.Byte1);
            Stream.WriteByte(union.Byte2);
            Stream.WriteByte(union.Byte3);
            Stream.WriteByte(union.Byte4);
            Stream.WriteByte(union.Byte5);
            Stream.WriteByte(union.Byte6);
            Stream.WriteByte(union.Byte7);
        }

        public void Write(decimal value)
        {
            var ii = decimal.GetBits(value);
            Write(ii[0]);
            Write(ii[1]);
            Write(ii[2]);
            Write(ii[3]);
        }

        public void WriteVInt(long value)
        {
            Stream.WriteVInt(value);
        }

        public void WriteVInt(ulong value)
        {
            Stream.WriteVUInt(value);
        }

        public void WriteVInt(long? value)
        {
            Stream.WriteVNInt(value);
        }

        public void WriteVInt(ulong? value)
        {
            Stream.WriteVNUInt(value);
        }
#pragma warning restore 1591 // XML Comments
    }
}
