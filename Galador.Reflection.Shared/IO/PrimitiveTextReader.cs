using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Galador.Reflection.IO
{
    /// <summary>
    /// An <see cref="IPrimitiveReader"/> reading from a <see cref="TextReader"/>.
    /// </summary>
    public class PrimitiveTextReader : IPrimitiveReader
    {
#pragma warning disable 1591 // XML Comments
        TextReader Reader;

        public PrimitiveTextReader(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            Reader = reader;
        }
        public void Dispose() { Reader.Dispose(); }

        #region ReadNextToken() ReadUntilNext()

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string ReadNextToken()
        {
            var sb = new StringBuilder(32);
            var c = Reader.Peek();

            if (c == '"')
            {
                Reader.Read();
                var reading = true;
                while (reading)
                {
                    c = Reader.Read();
                    switch (c)
                    {
                        case -1:
                            throw new IOException("EOF");
                        case '\\':
                            var next = Reader.Read();
                            switch (next)
                            {
                                case -1:
                                    throw new IOException("EOF");
                                case '\\':
                                case '"':
                                    sb.Append((char)next);
                                    break;
                                default:
                                    throw new IOException("Invalid Token");
                            }
                            break;
                        case '"':
                            reading = false;
                            break;
                        default:
                            sb.Append((char)c);
                            break;
                    }
                }
            }
            else
            {
                while (true)
                {
                    c = Reader.Peek();
                    if (c == -1)
                        throw new IOException("EOF");
                    if (c == ',' || char.IsWhiteSpace((char)c))
                        break;
                    Reader.Read();
                    sb.Append((char)c);
                }
            }
            ReadUntilNext();
            return sb.Length > 0 ? sb.ToString() : "";
        } 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ReadUntilNext()
        {
            int comma = 0;
            while (true)
            {
                var c = Reader.Peek();
                if (c == -1)
                    return;
                if (char.IsWhiteSpace((char)c))
                {
                    Reader.Read();
                }
                else if (comma == 0 && c == ',')
                {
                    comma++;
                    Reader.Read();
                }
                else
                {
                    return;
                }
            }
        }

        #endregion

        public string ReadString()
        {
            var p = Reader.Peek();
            if (p < 0)
                throw new IOException("EOF");
            var s = ReadNextToken();
            if (p == '"')
                return s;
            if (s == "null")
                return null;
            throw new IOException("Invalid Token");
        }

        public byte[] ReadBytes()
        {
            var s = ReadNextToken();
            if (s == "null")
                return null;
            if (!s.StartsWith("x"))
                throw new IOException("Invalid Token");
            if (s.Length == 1)
                return Empty<byte>.Array;
            return Convert.FromBase64String(s.Substring(1));
        }

        public Guid ReadGuid()
        {
            var s = ReadNextToken();
            return Guid.Parse(s);
        }

        public bool ReadBool()
        {
            var s = ReadNextToken();
            switch (s)
            {
                case "true": return true;
                case "false": return false;
                default:
                    throw new IOException("Invalid Token");
            }
        }

        public char ReadChar()
        {
            var s = ReadString();
            return s[0];
        }

        public byte ReadByte()
        {
            var s = ReadNextToken();
            return byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
        }

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)ReadByte());
        }

        public short ReadInt16()
        {
            var s = ReadNextToken();
            return short.Parse(s);
        }

        public ushort ReadUInt16()
        {
            var s = ReadNextToken();
            return ushort.Parse(s);
        }

        public int ReadInt32()
        {
            var s = ReadNextToken();
            return int.Parse(s);
        }

        public uint ReadUInt32()
        {
            var s = ReadNextToken();
            return uint.Parse(s);
        }

        public long ReadInt64()
        {
            var s = ReadNextToken();
            return long.Parse(s);
        }

        public ulong ReadUInt64()
        {
            var s = ReadNextToken();
            return ulong.Parse(s);
        }

        public float ReadSingle()
        {
            var s = ReadNextToken();
            return float.Parse(s);
        }

        public double ReadDouble()
        {
            var s = ReadNextToken();
            return double.Parse(s);
        }

        public decimal ReadDecimal()
        {
            var s = ReadNextToken();
            return decimal.Parse(s);
        }

        public long ReadVInt()
        {
            return this.ReadInt64();
        }

        public ulong ReadVUInt()
        {
            return this.ReadUInt64();
        }

        public long? ReadVNInt()
        {
            var s = ReadNextToken();
            if (s == "null")
                return null;
            return long.Parse(s);
        }

        public ulong? ReadVNUInt()
        {
            var s = ReadNextToken();
            if (s == "null")
                return null;
            return ulong.Parse(s);
        }
#pragma warning restore 1591 // XML Comments
    }
}
