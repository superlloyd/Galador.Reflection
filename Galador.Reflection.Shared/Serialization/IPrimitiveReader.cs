using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public interface IPrimitiveReader : IDisposable
    {
        string ReadString();
        byte[] ReadBytes();
        Guid ReadGuid();
        bool ReadBool();
        char ReadChar();
        byte ReadByte();
        sbyte ReadSByte();
        short ReadInt16();
        ushort ReadUInt16();
        int ReadInt32();
        uint ReadUInt32();
        long ReadInt64();
        ulong ReadUInt64();
        float ReadSingle();
        double ReadDouble();
        decimal ReadDecimal();
        long ReadVInt();
        ulong ReadVUInt();
        long? ReadVNInt();
        ulong? ReadVNUInt();
    }
}
