using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;

namespace Galador.Reflection.IO
{
    enum TextPrimitiveSeparator
    {
        Space,
        NewLine,
        Tab,
        Comma,
    }

    /// <summary>
    /// An <see cref="IPrimitiveWriter"/> writing to a <see cref="TextWriter"/>.
    /// </summary>
    public class PrimitiveTextWriter : IPrimitiveWriter
    {
#pragma warning disable 1591 // XML Comments
        TextWriter Writer;

        public PrimitiveTextWriter(TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            Writer = writer;
        }
        public void Dispose() { Writer.Dispose(); }

        internal TextPrimitiveSeparator Separator { get; set; } = TextPrimitiveSeparator.Space;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void WriteSeparator()
        {
            switch (Separator)
            {
                default:
                case TextPrimitiveSeparator.Space:
                    Writer.Write(' ');
                    break;
                case TextPrimitiveSeparator.NewLine:
                    Writer.WriteLine();
                    break;
                case TextPrimitiveSeparator.Tab:
                    Writer.Write('\t');
                    break;
                case TextPrimitiveSeparator.Comma:
                    Writer.Write(", ");
                    break;
            }
        }

        public void Write(string value)
        {
            if (value == null)
            {
                Writer.Write("null");
            }
            else
            {
                Writer.Write('"');
                foreach (var @char in value.Cast<char>())
                {
                    switch (@char)
                    {
                        case '"':
                            Writer.Write("\\\"");
                            break;
                        case '\\':
                            Writer.Write("\\\\");
                            break;
                        default:
                            Writer.Write(@char);
                            break;
                    }
                }
                Writer.Write('"');
            }
            WriteSeparator();
        }

        public void Write(byte[] value)
        {
            if (value == null)
            {
                Writer.Write("null");
            }
            else
            {
                var s = Convert.ToBase64String(value);
                Writer.Write('x');
                Writer.Write(s);
            }
            WriteSeparator();
        }

        public void Write(Guid value)
        {
            var s = value.ToString();
            Writer.Write(s);
            WriteSeparator();
        }

        public void Write(bool value)
        {
            Writer.Write(value ? "true" : "false");
            WriteSeparator();
        }

        public void Write(char value)
        {
            Write(new string(value, 1));
        }

        public void Write(byte value)
        {
            Writer.Write(value.ToString("x"));
            WriteSeparator();
        }

        public void Write(sbyte value)
        {
            Writer.Write(value.ToString("x"));
            WriteSeparator();
        }

        public void Write(short value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(ushort value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(int value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(uint value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(long value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(ulong value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void Write(float value)
        {
            Writer.Write(value.ToString("R"));
            WriteSeparator();
        }

        public void Write(double value)
        {
            Writer.Write(value.ToString("R"));
            WriteSeparator();
        }

        public void Write(decimal value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void WriteVInt(long value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void WriteVInt(ulong value)
        {
            Writer.Write(value);
            WriteSeparator();
        }

        public void WriteVInt(long? value)
        {
            if (value == null)
            {
                Writer.Write("null");
            }
            else
            {
                Writer.Write(value.Value);
            }
            WriteSeparator();
        }

        public void WriteVInt(ulong? value)
        {
            if (value == null)
            {
                Writer.Write("null");
            }
            else
            {
                Writer.Write(value.Value);
            }
            WriteSeparator();
        }
#pragma warning restore 1591 // XML Comments
    }
}
