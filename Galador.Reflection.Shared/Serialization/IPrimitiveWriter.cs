using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public interface IPrimitiveWriter : IDisposable
    {
        void Write(string value);
        void Write(byte[] value);
        void Write(Guid value);
        void Write(bool value);
        void Write(char value);
        void Write(byte value);
        void Write(sbyte value);
        void Write(short value);
        void Write(ushort value);
        void Write(int value);
        void Write(uint value);
        void Write(long value);
        void Write(ulong value);
        void Write(float value);
        void Write(double value);
        void Write(decimal value);
        // those are expected to be small number to be written in the most compact way possible
        // use that for indexes / IDs / length
        void WriteVInt(long value);
        void WriteVInt(ulong value);
        void WriteVInt(long? value);
        void WriteVInt(ulong? value);
    }
}
