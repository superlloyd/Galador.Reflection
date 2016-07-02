﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Galador.Reflection.Utils;

namespace Galador.Reflection.Serialization
{
    public class PrimitiveBinaryReader : IPrimitiveReader
    {
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
            return BitConverter.ToChar(buf16, 0);
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
            return BitConverter.ToInt16(buf16, 0);
        }

        public ushort ReadUInt16()
        {
            ReadWellKnownBytes(2);
            return BitConverter.ToUInt16(buf16, 0);
        }

        public int ReadInt32()
        {
            ReadWellKnownBytes(4);
            return BitConverter.ToInt32(buf16, 0);
        }

        public uint ReadUInt32()
        {
            ReadWellKnownBytes(4);
            return BitConverter.ToUInt32(buf16, 0);
        }

        public long ReadInt64()
        {
            ReadWellKnownBytes(8);
            return BitConverter.ToInt64(buf16, 0);
        }

        public ulong ReadUInt64()
        {
            ReadWellKnownBytes(8);
            return BitConverter.ToUInt64(buf16, 0);
        }

        public float ReadSingle()
        {
            ReadWellKnownBytes(4);
            return BitConverter.ToSingle(buf16, 0);
        }

        public double ReadDouble()
        {
            ReadWellKnownBytes(8);
            return BitConverter.ToDouble(buf16, 0);
        }

        public decimal ReadDecimal()
        {
            ReadWellKnownBytes(16);
            var parts = new int[]
            {
                BitConverter.ToInt32(buf16, 0),
                BitConverter.ToInt32(buf16, 4),
                BitConverter.ToInt32(buf16, 8),
                BitConverter.ToInt32(buf16, 12),
            };
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
