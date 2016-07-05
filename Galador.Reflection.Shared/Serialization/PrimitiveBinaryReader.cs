using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Galador.Reflection.Utils;

namespace Galador.Reflection.Serialization
{
    public class PrimitiveBinaryReader : IPrimitiveReader
    {
        Union8 union;
        Stream Stream;
        byte[] buf16 = new byte[16];


        public PrimitiveBinaryReader(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            this.Stream = stream;
        }
        public void Dispose() { Stream.Dispose(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadWellKnownBytes(int n)
        {
            int pos = 0;
            while (pos < n)
            {
                var nRead = Stream.Read(buf16, pos, n - pos);
                if (nRead <= 0)
                    throw new IOException("EOF");
                pos += nRead;
            }
        }

        public string ReadString()
        {
            var buf = this.ReadBytes();
            if (buf == null)
                return null;
            if (buf.Length == 0)
                return string.Empty;
            return Encoding.UTF8.GetString(buf, 0, buf.Length);
        }

        public byte[] ReadBytes()
        {
            var len = Stream.ReadVNUInt();
            if (len == null)
                return null;
            if (len.Value == 0)
                return Empty<byte>.Array;
            var buf = new byte[(int)len.Value];
            int pos = 0;
            while (pos < buf.Length)
            {
                var nRead = Stream.Read(buf, pos, buf.Length - pos);
                if (nRead <= 0)
                    throw new IOException("EOF");
                pos += nRead;
            }
            return buf;
        }

        public Guid ReadGuid()
        {
            ReadWellKnownBytes(16);
            return new Guid(buf16);
        }

        public bool ReadBool()
        {
            var b = this.ReadByte();
            return b != 0;
        }

        public char ReadChar()
        {
            ReadWellKnownBytes(2);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            return union.Char;
        }

        public byte ReadByte()
        {
            var b = Stream.ReadByte();
            if (b < 0)
                throw new IOException("EOF");
            return (byte)b;
        }

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)this.ReadByte());
        }

        public short ReadInt16()
        {
            ReadWellKnownBytes(2);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            return union.Int16;
        }

        public ushort ReadUInt16()
        {
            ReadWellKnownBytes(2);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            return union.UInt16;
        }

        public int ReadInt32()
        {
            ReadWellKnownBytes(4);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            return union.Int32;
        }

        public uint ReadUInt32()
        {
            ReadWellKnownBytes(4);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            return union.UInt32;
        }

        public long ReadInt64()
        {
            ReadWellKnownBytes(8);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            union.Byte4 = buf16[4];
            union.Byte5 = buf16[5];
            union.Byte6 = buf16[6];
            union.Byte7 = buf16[7];
            return union.Int64;
        }

        public ulong ReadUInt64()
        {
            ReadWellKnownBytes(8);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            union.Byte4 = buf16[4];
            union.Byte5 = buf16[5];
            union.Byte6 = buf16[6];
            union.Byte7 = buf16[7];
            return union.UInt64;
        }

        public float ReadSingle()
        {
            ReadWellKnownBytes(4);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            return union.Single;
        }

        public double ReadDouble()
        {
            ReadWellKnownBytes(8);
            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            union.Byte4 = buf16[4];
            union.Byte5 = buf16[5];
            union.Byte6 = buf16[6];
            union.Byte7 = buf16[7];
            return union.Double;
        }

        public decimal ReadDecimal()
        {
            ReadWellKnownBytes(16);
            var parts = new int[4];

            union.Byte0 = buf16[0];
            union.Byte1 = buf16[1];
            union.Byte2 = buf16[2];
            union.Byte3 = buf16[3];
            parts[0] = union.Int32;

            union.Byte0 = buf16[4];
            union.Byte1 = buf16[5];
            union.Byte2 = buf16[6];
            union.Byte3 = buf16[7];
            parts[1] = union.Int32;

            union.Byte0 = buf16[8];
            union.Byte1 = buf16[9];
            union.Byte2 = buf16[10];
            union.Byte3 = buf16[11];
            parts[2] = union.Int32;

            union.Byte0 = buf16[12];
            union.Byte1 = buf16[13];
            union.Byte2 = buf16[14];
            union.Byte3 = buf16[15];
            parts[3] = union.Int32;

            return new decimal(parts);
        }

        public long ReadVInt()
        {
            return Stream.ReadVInt();
        }

        public ulong ReadVUInt()
        {
            return Stream.ReadVUInt();
        }

        public long? ReadVNInt()
        {
            return Stream.ReadVNInt();
        }

        public ulong? ReadVNUInt()
        {
            return Stream.ReadVNUInt();
        }
    }
}
