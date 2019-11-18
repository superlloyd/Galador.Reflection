using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization.IO
{
    /// <summary>
    /// An interface that know how to write all primitive type (i.e. all non object <see cref="PrimitiveType"/>) to some underlying storage.
    /// </summary>
    public interface IPrimitiveWriter : IDisposable
    {
#pragma warning disable 1591 // XML Comments
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
#pragma warning restore 1591 // XML Comments

        /// <summary>
        /// Writes an <c>long</c> in a more compact format.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void WriteVInt(long value);
        /// <summary>
        /// Writes an <c>ulong</c> in a more compact format.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void WriteVInt(ulong value);
        /// <summary>
        /// Writes an <c>long?</c> in a more compact format.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void WriteVInt(long? value);
        /// <summary>
        /// Writes an <c>ulong?</c> in a more compact format.
        /// </summary>
        /// <param name="value">The value to write.</param>
        void WriteVInt(ulong? value);
    }
}
