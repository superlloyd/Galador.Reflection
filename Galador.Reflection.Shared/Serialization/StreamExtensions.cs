using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Galador.Reflection.Utils;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// A utility class helping write base .NET value to a stream
    /// </summary>
    public static class StreamExtensions
    {
        #region (Read/Write)V(N)(U)Int()

        /// <summary>
        /// Write long as variable size array. The smaller the value, the less byte will be needed.
        /// </summary>
        public static int WriteVInt(this Stream ins, long l)
        {
            bool isNeg = l < 0;
            if (isNeg)
                l = ~l;
            var ul = unchecked((ulong)l);
            int count = 0;
            bool first = true;
            do
            {
                byte part;
                if (first)
                {
                    first = false;
                    part = (byte)(ul & 0x3F);
                    if (isNeg)
                        part |= 0x40;
                    ul = ul >> 6;
                }
                else
                {
                    part = (byte)(ul & 0x7F);
                    ul = ul >> 7;
                }
                if (ul > 0)
                    part |= 0x80;
                ins?.WriteByte(part);
                count++;
            }
            while (ul > 0);
            return count;
        }

        /// <summary>
        /// Read value stored by <see cref="WriteVInt(Stream, long)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <returns>The <c>long</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static long ReadVInt(this Stream ins)
        {
            int count;
            return ReadVInt(ins, out count);
        }
        /// <summary>
        /// Read value stored by <see cref="WriteVInt(Stream, long)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <param name="count">How many bytes were used to store the value.</param>
        /// <returns>The <c>long</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static long ReadVInt(this Stream ins, out int count)
        {
            count = 0;
            ulong ul = 0;
            int shift = 0;
            bool first = true;
            bool isNeg = false;
            while (true)
            {
                var part = ins.ReadByte();
                count++;
                if (part < 0)
                    throw new IOException("EOF");
                if (first)
                {
                    first = false;
                    isNeg = (part & 0x40) == 0x40; 
                    ul = (ulong)(part & 0x3F);
                    shift += 6;
                }
                else
                {
                    ul |= (ulong)(part & 0x7F) << shift;
                    shift += 7;
                }
                if ((part & 0x80) == 0)
                {
                    var l = unchecked((long)ul);
                    if (isNeg)
                        l = ~l;
                    return l;
                }
                if (shift > 63)
                    throw new IOException("Corrupted Data");
            }
        }

        /// <summary>
        /// Write a nullable long as variable size array. The smaller the value, the less byte will be needed.
        /// </summary>
        public static int WriteVNInt(this Stream ins, long? nl)
        {
            int count = 0;
            if (nl.HasValue)
            {
                var l = nl.Value;
                bool isNeg = l < 0;
                if (isNeg)
                    l = ~l;
                var ul = unchecked((ulong)l);
                bool first = true;
                do
                {
                    byte part;
                    if (first)
                    {
                        first = false;
                        part = (byte)(ul & 0x1F);
                        part |= 0x40;
                        if (isNeg)
                            part |= 0x20;
                        ul = ul >> 5;
                    }
                    else
                    {
                        part = (byte)(ul & 0x7F);
                        ul = ul >> 7;
                    }
                    if (ul > 0)
                        part |= 0x80;
                    ins?.WriteByte(part);
                    count++;
                }
                while (ul > 0);
            }
            else
            {
                ins?.WriteByte(0);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Read value stored by <see cref="WriteVNInt(Stream, long?)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <returns>The <c>long?</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static long? ReadVNInt(this Stream ins)
        {
            int count;
            return ReadVNInt(ins, out count);
        }
        /// <summary>
        /// Read value stored by <see cref="WriteVNInt(Stream, long?)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <param name="count">How many bytes were used to store the value.</param>
        /// <returns>The <c>long?</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static long? ReadVNInt(this Stream ins, out int count)
        {
            count = 0;
            ulong ul = 0;
            int shift = 0;
            bool first = true;
            bool isNeg = false;
            while (true)
            {
                var part = ins.ReadByte();
                count++;
                if (part < 0)
                    throw new IOException("EOF");
                if (first)
                {
                    first = false;
                    if (part == 0)
                        return null;
                    isNeg = (part & 0x20) == 0x20;
                    ul = (ulong)(part & 0x1F);
                    shift += 5;
                }
                else
                {
                    ul |= (ulong)(part & 0x7F) << shift;
                    shift += 7;
                }
                if ((part & 0x80) == 0)
                {
                    var l = unchecked((long)ul);
                    if (isNeg)
                        l = ~l;
                    return l;
                }
                if (shift > 63)
                    throw new IOException("Corrupted Data");
            }
        }

        /// <summary>
        /// Write ulong as variable size array. The smaller the value, the less byte will be needed.
        /// </summary>
        public static int WriteVUInt(this Stream ins, ulong ul)
        {
            int count = 0;
            do
            {
                var part = (byte)(ul & 0x7F);
                if (ul > 0x7F)
                    part |= 0x80;
                ul = ul >> 7;
                ins?.WriteByte(part);
                count++;
            }
            while (ul > 0);
            return count;
        }

        /// <summary>
        /// Read value stored by <see cref="WriteVUInt(Stream, ulong)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <returns>The <c>ulong</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static ulong ReadVUInt(this Stream ins)
        {
            int count;
            return ReadVUInt(ins, out count);
        }
        /// <summary>
        /// Read value stored by <see cref="WriteVUInt(Stream, ulong)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <param name="count">How many bytes were used to store the value.</param>
        /// <returns>The <c>ulong</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static ulong ReadVUInt(this Stream ins, out int count)
        {
            count = 0;
            ulong ul = 0;
            int shift = 0;
            while (true)
            {
                var part = ins.ReadByte();
                count++;
                if (part < 0)
                    throw new IOException("EOF");
                ul |= (ulong)(part & 0x7F) << shift;
                shift += 7;
                if ((part & 0x80) == 0)
                    return ul;
                if (shift > 63)
                    throw new IOException("Corrupted Data");
            }
        }

        /// <summary>
        /// Write a nullable long as variable size array. The smaller the value, the less byte will be needed.
        /// </summary>
        public static int WriteVNUInt(this Stream ins, ulong? nul)
        {
            int count = 0;
            if (nul.HasValue)
            {
                var ul = nul.Value;
                bool first = true;
                do
                {
                    byte part;
                    if (first)
                    {
                        first = false;
                        part = (byte)(ul & 0x3F);
                        part |= 0x40;
                        ul = ul >> 6;
                    }
                    else
                    {
                        part = (byte)(ul & 0x7F);
                        ul = ul >> 7;
                    }
                    if (ul > 0)
                        part |= 0x80;
                    ins?.WriteByte(part);
                    count++;
                }
                while (ul > 0);
            }
            else
            {
                ins?.WriteByte(0);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Read value stored by <see cref="WriteVNUInt(Stream, ulong?)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <returns>The <c>ulong?</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static ulong? ReadVNUInt(this Stream ins)
        {
            int count;
            return ReadVNUInt(ins, out count);
        }
        /// <summary>
        /// Read value stored by <see cref="WriteVNUInt(Stream, ulong?)"/>
        /// </summary>
        /// <param name="ins">The stream to read the value from.</param>
        /// <param name="count">How many bytes were used to store the value.</param>
        /// <returns>The <c>ulong?</c> just read.</returns>
        /// <exception cref="System.IO.IOException">EOF or Corrupted Data</exception>
        public static ulong? ReadVNUInt(this Stream ins, out int count)
        {
            count = 0;
            ulong ul = 0;
            int shift = 0;
            bool first = true;
            while (true)
            {
                var part = ins.ReadByte();
                count++;
                if (part < 0)
                    throw new IOException("EOF");
                if (first)
                {
                    first = false;
                    if (part == 0)
                        return null;
                    ul = (ulong)(part & 0x3F);
                    shift += 6;
                }
                else
                {
                    ul |= (ulong)(part & 0x7F) << shift;
                    shift += 7;
                }
                if ((part & 0x80) == 0)
                    return ul;
                if (shift > 63)
                    throw new IOException("Corrupted Data");
            }
        }

        #endregion
    }
}
