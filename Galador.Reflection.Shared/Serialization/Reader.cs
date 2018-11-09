using Galador.Reflection.Serialization.IO;
using Galador.Reflection.Utils;
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
        readonly ulong VERSION;
        bool readData;

        public Reader(IPrimitiveReader input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.VERSION = input.ReadVUInt();
            switch (VERSION)
            {
                case 0x01_02:
                    break;
                default:
                    throw new ArgumentException($"Unsupported version number {VERSION:X4}");
            }
        }

        public void Dispose()
        {
            input.Dispose();
        }

        public object ReadData() => Read(true);

        public object Read() => Read(false);

        object Read(bool readRaw)
        {
            if (readRecurseDepth++ == 0)
            {
                this.readData = readRaw;
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
            var actual = expected;
            if (expected.IsReference && !expected.IsSealed)
                actual = (TypeData)Read(RType.TypeData(), null);

            object ReturnRegister(object value)
            {
                if (oid != 0)
                    Register(oid, value);
                return value;
            }

            // only proceed further is supported
            if (!expected.IsSupported || !actual.IsSupported)
                return ReturnRegister(new ObjectData(actual));

            if (actual.IsISerializable && !settings.IgnoreISerializable)
            {
                return ReturnRegister(ReadISerializable(actual, expected, possible));
            }
            else if (actual.HasConverter && !settings.IgnoreTypeConverter)
            {
                return ReturnRegister(ReadConverter(actual, expected));
            }
            else if (actual.HasSurrogate)
            {
                return ReturnRegister(ReadSurrogate(actual, expected));
            }
            else
            {
                switch (actual.Kind)
                {
                    default:
                    case PrimitiveType.None:
                        throw new InvalidOperationException("shouldn't be there");
                    case PrimitiveType.Object:
                        if (actual.IsArray)
                        {
                            return ReadArray(actual, expected, oid);
                        }
                        else if (actual.IsNullable)
                        {
                            return ReturnRegister(Read(actual.Element, null));
                        }
                        else if (actual.IsEnum)
                        {
                            var val = Read(actual.Element, null);
                            // TODO
                            return val;
                        }
                        else
                        {
                            return ReturnRegister(ReadObject(actual, expected, oid, possible));
                        }
                    case PrimitiveType.Type:
                        {
                            var result = new TypeData();
                            ReturnRegister(result);
                            result.Read(this, input);
                            return result;
                        }
                    case PrimitiveType.String:
                        return ReturnRegister(input.ReadString());
                    case PrimitiveType.Bytes:
                        return ReturnRegister(input.ReadBytes());
                    case PrimitiveType.Guid:
                        return ReturnRegister(input.ReadGuid());
                    case PrimitiveType.Bool:
                        return ReturnRegister(input.ReadBool());
                    case PrimitiveType.Char:
                        return ReturnRegister(input.ReadChar());
                    case PrimitiveType.Byte:
                        return ReturnRegister(input.ReadByte());
                    case PrimitiveType.SByte:
                        return ReturnRegister(input.ReadSByte());
                    case PrimitiveType.Int16:
                        return ReturnRegister(input.ReadInt16());
                    case PrimitiveType.UInt16:
                        return ReturnRegister(input.ReadUInt16());
                    case PrimitiveType.Int32:
                        return ReturnRegister(input.ReadInt32());
                    case PrimitiveType.UInt32:
                        return ReturnRegister(input.ReadUInt32());
                    case PrimitiveType.Int64:
                        return ReturnRegister(input.ReadInt64());
                    case PrimitiveType.UInt64:
                        return ReturnRegister(input.ReadUInt64());
                    case PrimitiveType.Single:
                        return ReturnRegister(input.ReadSingle());
                    case PrimitiveType.Double:
                        return ReturnRegister(input.ReadDouble());
                    case PrimitiveType.Decimal:
                        return ReturnRegister(input.ReadDecimal());
                }
            }
        }

        object ReadISerializable(TypeData actual, TypeData expected, object possibleValue)
        {
            var info = new SRS.SerializationInfo(typeof(object), new SRS.FormatterConverter());
            var ctx = new SRS.StreamingContext(SRS.StreamingContextStates.Persistence);
            var N = (int)input.ReadVInt();
            for (int i = 0; i < N; i++)
            {
                var s = (string)Read(RString.TypeData(), null);
                var o = Read(RObject.TypeData(), null);
                info.AddValue(s, o);
            }

            if (possibleValue != null)
            {
                var ctor = possibleValue.GetType().TryGetConstructors(info.GetType(), ctx.GetType()).FirstOrDefault();
                // No FastMethod(): couldn't manage to call constructor on existing instance
                // Also, dare to do call constructor on existing instance!!
                if (ctor != null)
                {
                    ctor.Invoke(possibleValue, new object[] { info, ctx });
                    return possibleValue;
                }
                else
                {
                    Log.Warning($"ignored ISerializable data");
                    return possibleValue;
                }
            }

            if (actual.Target(!readData) != null)
            {
                var ctor = actual.Target(!readData)?.Type.TryGetConstructors(info.GetType(), ctx.GetType()).FirstOrDefault();
                if (ctor != null)
                {
                    possibleValue = ctor.Invoke(null, new object[] { info, ctx }); // Dare to do it! Call constructor on existing instance!!
                    return possibleValue;
                }

                // should we do that?
                possibleValue = actual.Target(!readData)?.FastType.TryConstruct();
                if (possibleValue != null)
                {
                    Log.Warning($"ignored ISerializable data");
                    return possibleValue;
                }
            }

            return new ObjectData(actual)
            { 
                Info = info,
            };
        }

        object ReadConverter(TypeData actual, TypeData expected)
        {
            var s = (string)Read(RString.TypeData(), null);

            var converter = actual.Target(!readData)?.Converter;
            if (converter != null)
                return converter.ConvertFromInvariantString(s);

            return new ObjectData(actual)
            {
                ConverterString = s,
            };
        }

        object ReadSurrogate(TypeData actual, TypeData expected)
        {
            var o = Read(RObject.TypeData(), null);

            var surrogate = actual.Target(!readData)?.Surrogate;
            if (surrogate != null)
            {
                return surrogate.Revert(o);
            }

            return new ObjectData(actual)
            {
                SurrogateObject = o,
            };
        }

        object ReadArray(TypeData actual, TypeData expected, ulong oid)
        {
            var ranks = Enumerable.Range(0, actual.ArrayRank)
                .Select(x => (int)input.ReadVInt())
                .ToArray();

            Array array;
            var et = actual.Element.Target(!readData);
            if (et != null)
            {
                array = Array.CreateInstance(et.Type, ranks);
            }
            else
            {
                array = Array.CreateInstance(typeof(ObjectData), ranks);
            }
            Register(oid, array);

            if (ranks.All(x => x > 0))
            {
                var indices = new int[ranks.Length];
                do
                {
                    var value = Read(actual.Element, null);
                    array.SetValue(value, indices);
                }
                while (Inc());

                bool Inc()
                {
                    for (int i = ranks.Length - 1; i >= 0; i--)
                    {
                        indices[i]++;
                        if (indices[i] < ranks[i])
                            return true;
                        indices[i] = 0;
                    }
                    return false;
                }
            }

            return array;
        }

        object ReadObject(TypeData actual, TypeData expected, ulong oid, object possible)
        {

        }
    }
}
