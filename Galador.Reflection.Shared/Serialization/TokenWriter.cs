using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Galador.Reflection.Serialization
{
    public class TokenPrimitiveWriter : IPrimitiveWriter
    {
        List<object> stream;
        public TokenPrimitiveWriter(List<object> stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            this.stream = stream;
        }
        public TokenPrimitiveWriter() { this.stream = new List<object>(256); }

        public List<object> TokenStream { get { return stream; } }

        void IDisposable.Dispose() { }
        void IPrimitiveWriter.Write(char value) { stream.Add(value); }
        void IPrimitiveWriter.Write(sbyte value) { stream.Add(value); }
        void IPrimitiveWriter.Write(ushort value) { stream.Add(value); }
        void IPrimitiveWriter.Write(uint value) { stream.Add(value); }
        void IPrimitiveWriter.Write(ulong value) { stream.Add(value); }
        void IPrimitiveWriter.Write(double value) { stream.Add(value); }
        void IPrimitiveWriter.Write(decimal value) { stream.Add(value); }
        void IPrimitiveWriter.Write(float value) { stream.Add(value); }
        void IPrimitiveWriter.Write(long value) { stream.Add(value); }
        void IPrimitiveWriter.Write(int value) { stream.Add(value); }
        void IPrimitiveWriter.Write(short value) { stream.Add(value); }
        void IPrimitiveWriter.Write(byte value) { stream.Add(value); }
        void IPrimitiveWriter.Write(bool value) { stream.Add(value); }
        void IPrimitiveWriter.Write(Guid value) { stream.Add(value); }
        void IPrimitiveWriter.Write(byte[] value) { stream.Add(value); }
        void IPrimitiveWriter.Write(string value) { stream.Add(value); }
        void IPrimitiveWriter.WriteVInt(ulong? value) { stream.Add(value); }
        void IPrimitiveWriter.WriteVInt(long? value) { stream.Add(value); }
        void IPrimitiveWriter.WriteVInt(ulong value) { stream.Add(value); }
        void IPrimitiveWriter.WriteVInt(long value) { stream.Add(value); }
    }

    public class TokenPrimitiveReader : IPrimitiveReader
    {
        List<object> stream;
        public TokenPrimitiveReader(List<object> stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            this.stream = stream;
        }
        public int Position
        {
            get { return position; }
            set
            {
                if (value < 0 || value >= stream.Count)
                    throw new ArgumentOutOfRangeException();
                position = value;
            }
        }
        int position;
        public List<object> TokenStream { get { return stream; } }

        string IPrimitiveReader.ReadString() { return (string)stream[position++]; }
        byte[] IPrimitiveReader.ReadBytes() { return (byte[])stream[position++]; }
        Guid IPrimitiveReader.ReadGuid() { return (Guid)stream[position++]; }
        bool IPrimitiveReader.ReadBool() { return (bool)stream[position++]; }
        char IPrimitiveReader.ReadChar() { return (char)stream[position++]; }
        byte IPrimitiveReader.ReadByte() { return (byte)stream[position++]; }
        sbyte IPrimitiveReader.ReadSByte() { return (sbyte)stream[position++]; }
        short IPrimitiveReader.ReadInt16() { return (short)stream[position++]; }
        ushort IPrimitiveReader.ReadUInt16() { return (ushort)stream[position++]; }
        int IPrimitiveReader.ReadInt32() { return (int)stream[position++]; }
        uint IPrimitiveReader.ReadUInt32() { return (uint)stream[position++]; }
        long IPrimitiveReader.ReadInt64() { return (long)stream[position++]; }
        ulong IPrimitiveReader.ReadUInt64() { return (ulong)stream[position++]; }
        float IPrimitiveReader.ReadSingle() { return (float)stream[position++]; }
        double IPrimitiveReader.ReadDouble() { return (double)stream[position++]; }
        decimal IPrimitiveReader.ReadDecimal() { return (decimal)stream[position++]; }
        long IPrimitiveReader.ReadVInt() { return (long)stream[position++]; }
        ulong IPrimitiveReader.ReadVUInt() { return (ulong)stream[position++]; }
        long? IPrimitiveReader.ReadVNInt() { return (long?)stream[position++]; }
        ulong? IPrimitiveReader.ReadVNUInt() { return (ulong?)stream[position++]; }
        void IDisposable.Dispose() { }
    }
}
