using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.IO
{
    /// <summary>
    /// An interface that know how to read all primitive type (i.e. all non object <see cref="PrimitiveType"/>) from some underlying storage.
    /// </summary>
    public interface IPrimitiveReader : IDisposable
    {
#pragma warning disable 1591 // XML Comments
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
#pragma warning restore 1591 // XML Comments

        /// <summary>
        /// Reads an <c>long</c> that was written in a more compact format.
        /// </summary>
        /// <returns>The <c>long</c> that was read.</returns>
        long ReadVInt();
        /// <summary>
        /// Reads an <c>ulong</c> that was written in a more compact format.
        /// </summary>
        /// <returns>The <c>ulong</c> that was read.</returns>
        ulong ReadVUInt();
        /// <summary>
        /// Reads an <c>long?</c> that was written in a more compact format.
        /// </summary>
        /// <returns>The <c>long?</c> that was read.</returns>
        long? ReadVNInt();
        /// <summary>
        /// Reads an <c>ulong?</c> that was written in a more compact format.
        /// </summary>
        /// <returns>The <c>ulong?</c> that was read.</returns>
        ulong? ReadVNUInt();
    }
}
