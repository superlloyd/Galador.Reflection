using Galador.Reflection.Serialization.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SRS = System.Runtime.Serialization;


namespace Galador.Reflection.Serialization
{
    public class Reader : Context, IDisposable
    {
        readonly IPrimitiveReader input;
        readonly internal SerializationSettings settings = new SerializationSettings();

        public Reader(IPrimitiveReader input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public void Dispose()
        {
            input.Dispose();
        }

        public object Read()
        {
            if (readRecurseDepth++ == 0)
            {
                var sFlags = input.ReadVInt();
                settings.FromFlags((int)sFlags);
            }
            try
            {
                return Read(RObject.TypeData(), null);
            }
            finally
            {
                if (--readRecurseDepth == 0)
                {
                    foreach (var item in Objects.OfType<IDeserialized>())
                        item.Deserialized();
                    foreach (var item in Objects.OfType<SRS.IDeserializationCallback>())
                        item.OnDeserialization(this);
                }
            }
        }
        int readRecurseDepth = 0;

        internal object Read(TypeData expected, object possible)
        {
            // check id first
            ulong oid = 0;
            if (expected.IsReference)
            {
                oid = input.ReadVUInt();
                if (TryGetObject(oid, out var result))
                    return result;
            }

            // if expected is not final
            if (expected.IsReference && !expected.IsSealed)
            {

            }

            // only proceed further is supported
        }
    }
}
