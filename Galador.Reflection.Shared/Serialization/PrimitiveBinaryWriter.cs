using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using Galador.Reflection.Utils;

namespace Galador.Reflection.Serialization
{
    public class PrimitiveBinaryWriter : IPrimitiveWriter
    {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteKnownBytes(byte[] buf) { Stream.Write(buf, 0, buf.Length); }

        public void Write(Guid value)
        {
            WriteKnownBytes(value.ToByteArray());
        }

        public void Write(bool value)
        {
            Stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public void Write(char value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
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
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(ushort value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(int value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(uint value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(long value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(ulong value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(float value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
        }

        public void Write(double value)
        {
            WriteKnownBytes(BitConverter.GetBytes(value));
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
    }
}
