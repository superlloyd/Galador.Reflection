using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Galador.Reflection.Serialization.IO
{
    /// <summary>
    /// An <see cref="IPrimitiveWriter"/> that write data to an object list. It is faster for cloning and also make it easier to debug serialization.
    /// </summary>
    public class TokenPrimitiveWriter : IPrimitiveWriter
    {
        List<object> stream;

        /// <summary>
        /// Create a new <see cref="TokenPrimitiveWriter"/> from a list.
        /// </summary>
        /// <param name="stream">The target list, where data will be written.</param>
        public TokenPrimitiveWriter(List<object> stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            this.stream = stream;
        }
        /// <summary>
        /// Create a new <see cref="TokenPrimitiveWriter"/> and its underlying list.
        /// </summary>
        public TokenPrimitiveWriter() { this.stream = new List<object>(256); }

#if DEBUG
        internal void DebugInfo(string s)
        {
            debugInfo.TryGetValue(stream.Count, out var prev);
            if (prev != null) debugInfo[stream.Count] = prev + "\r\n" + s;
            else debugInfo[stream.Count] = s;
        }
        Dictionary<int, string> debugInfo = new Dictionary<int, string>();
        public IEnumerable<(object token, string info)> DebugTokenInfo
        {
            get
            {
                for (int i = 0; i < stream.Count; i++)
                {
                    debugInfo.TryGetValue(i, out var s);
                    yield return (stream[i], s);
                }
            }
        }
#endif

        /// <summary>
        /// The list where token will be written.
        /// </summary>
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

    /// <summary>
    /// An <see cref="IPrimitiveReader"/> that read the data from an object list. It is faster for cloning and also make it easier to debug serialization.
    /// </summary>
    public class TokenPrimitiveReader : IPrimitiveReader
    {
        List<object> stream;

        /// <summary>
        /// Construct a new <see cref="TokenPrimitiveReader"/>
        /// </summary>
        /// <param name="stream">The source of token</param>
        public TokenPrimitiveReader(List<object> stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            this.stream = stream;
        }
        /// <summary>
        /// Current position in the list of token.
        /// </summary>
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

        /// <summary>
        /// The source of values.
        /// </summary>
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
