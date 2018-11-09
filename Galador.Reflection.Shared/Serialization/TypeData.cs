using Galador.Reflection.Serialization.IO;
using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Galador.Reflection.Serialization
{
    public sealed class TypeData
    {
        internal TypeData() { }

        #region Initialize(RuntimeType)

        internal void Initialize(RuntimeType type)
        {
            target = type;

            IsSupported = type.IsSupported;
            if (!IsSupported)
                return;

            Kind = type.Kind;
            switch (type.Kind)
            {
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    break;
                case PrimitiveType.Type:
                case PrimitiveType.String:
                case PrimitiveType.Bytes:
                    IsReference = true;
                    IsSealed = true;
                    return;
                default:
                    return;
            }

            BaseType = type.BaseType?.TypeData();
            Element = type.Element?.TypeData();
            FullName = type.FullName;
            Assembly = type.Assembly;

            IsSealed = type.IsSealed;
            IsReference = type.IsReference;
            IsEnum = type.IsEnum;
            IsInterface = type.IsInterface;
            HasConverter = type.Converter != null;
            HasSurrogate = type.Surrogate != null;
            IsISerializable = type.IsISerializable;
            IsGenericParameter = type.IsGenericParameter;
            IsGeneric = type.IsGeneric;
            IsGenericTypeDefinition = type.IsGenericTypeDefinition;
            IsNullable = type.IsNullable;

            GenericParameterIndex = type.GenericParameterIndex;
            if (type.GenericParameters != null)
                GenericParameters = type.GenericParameters.Select(x => x.TypeData()).ToList().AsReadOnly();

            if (!HasSurrogate && !HasSurrogate
                && !IsInterface && !IsISerializable
                && !IsArray && !IsEnum)
            {
                foreach (var m in type.Members)
                {
                    Members.Add(new Member
                    {
                        Name = m.Name,
                        Type = m.Type.TypeData(),
                    });
                }
                CollectionType = type.CollectionType;
                Collection1 = type.Collection1?.TypeData();
                Collection2 = type.Collection2?.TypeData();
            }
        }

        #endregion

        #region Read() Write()

        internal void Read(Reader reader, IPrimitiveReader input)
        {
            var flags = input.ReadVInt();
            if (flags == 0)
            {
                IsSupported = false;
                return;
            }
            IsInterface = (flags & (1 << 1)) != 0;
            IsISerializable = (flags & (1 << 2)) != 0;
            IsReference = (flags & (1 << 3)) != 0;
            IsSealed = (flags & (1 << 4)) != 0;
            IsArray = (flags & (1 << 5)) != 0;
            IsNullable = (flags & (1 << 6)) != 0;
            IsEnum = (flags & (1 << 7)) != 0;
            IsGeneric = (flags & (1 << 8)) != 0;
            IsGenericParameter = (flags & (1 << 9)) != 0;
            IsGenericTypeDefinition = (flags & (1 << 10)) != 0;
            HasConverter = (flags & (1 << 11)) != 0;
            HasSurrogate = (flags & (1 << 12)) != 0;
            Kind = (PrimitiveType)((flags >> 13) & 0b11111);
            CollectionType = (RuntimeCollectionType)((flags >> 18) & 0b111);
            switch (Kind)
            {
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    break;
                default:
                    return;
            }
            FullName = (string)reader.Read(Context.RString.TypeData(), null);
            Assembly = (string)reader.Read(Context.RString.TypeData(), null);
            GenericParameterIndex = (int)input.ReadVInt();
            int genCount = (int)input.ReadVInt();
            if (genCount > 0)
            {
                var glp = new List<TypeData>();
                for (int i = 0; i < genCount; i++)
                {
                    var data = (TypeData)reader.Read(Context.RType.TypeData(), null);
                    glp.Add(data);
                }
                GenericParameters = glp.AsReadOnly();
            }
            Element = (TypeData)reader.Read(Context.RType.TypeData(), null);

            if (reader.settings.SkipMemberData)
                return;
            BaseType = (TypeData)reader.Read(Context.RType.TypeData(), null);
            if (!HasSurrogate && !HasSurrogate
                && !IsInterface && !IsISerializable
                && !IsArray && !IsEnum)
            {
                int mc = (int)input.ReadVInt();
                for (int i = 0; i < mc; i++)
                {
                    var m = new Member
                    {
                        Name = (string)reader.Read(Context.RString.TypeData(), null),
                        Type = (TypeData)reader.Read(Context.RType.TypeData(), null),
                    };
                    Members.Add(m);
                }
                Collection1 = (TypeData)reader.Read(Context.RType.TypeData(), null);
                Collection2 = (TypeData)reader.Read(Context.RType.TypeData(), null);
            }
        }

        internal void Write(Writer writer, IPrimitiveWriter output)
        {
            if (!IsSupported)
            {
                output.WriteVInt(0);
                return;
            }
            var flags = 1;
            if (IsInterface) flags |= 1 << 1;
            if (IsISerializable) flags |= 1 << 2;
            if (IsReference) flags |= 1 << 3;
            if (IsSealed) flags |= 1 << 4;
            if (IsArray) flags |= 1 << 5;
            if (IsNullable) flags |= 1 << 6;
            if (IsEnum) flags |= 1 << 7;
            if (IsGeneric) flags |= 1 << 8;
            if (IsGenericParameter) flags |= 1 << 9;
            if (IsGenericTypeDefinition) flags |= 1 << 10;
            if (HasConverter) flags |= 1 << 11;
            if (HasSurrogate) flags |= 1 << 12;
            flags |= (int)Kind << 13;
            flags |= (int)CollectionType << 18;
            switch (Kind)
            {
                case PrimitiveType.None:
                case PrimitiveType.Object:
                    break;
                default:
                    return;
            }
            output.WriteVInt(flags);
            writer.Write(Context.RString, FullName);
            writer.Write(Context.RString, Assembly);
            output.WriteVInt(GenericParameterIndex);
            output.WriteVInt(GenericParameters?.Count ?? 0);
            if (GenericParameters != null)
                for (int i = 0; i < GenericParameters.Count; i++)
                    writer.Write(Context.RType, GenericParameters[i]);
            writer.Write(Context.RType, Element);

            if (writer.Settings.SkipMemberData)
                return;
            writer.Write(Context.RType, BaseType);
            if (!HasSurrogate && !HasSurrogate
                && !IsInterface && !IsISerializable
                && !IsArray && !IsEnum)
            {
                output.WriteVInt(Members.Count);
                for (int i = 0; i < Members.Count; i++)
                {
                    var m = Members[i];
                    writer.Write(Context.RString, m.Name);
                    writer.Write(Context.RType, m.Type);
                }
                writer.Write(Context.RType, Collection1);
                writer.Write(Context.RType, Collection2);
            }
        }

        #endregion

        #region public: Target()

        public RuntimeType Target(bool resolve)
        {
            if (target == null && resolve && !resolved)
            {
                resolved = true;
                switch (Kind)
                {
                    case PrimitiveType.None:
                        break;
                    case PrimitiveType.Object:
                        // TODO
                        break;
                    default:
                        target = RuntimeType.GetType(PrimitiveConverter.GetType(Kind));
                        break;
                }
            }
            return target;
        }
        RuntimeType target;
        bool resolved;

        #endregion

        public bool IsSupported { get; private set; }
        public PrimitiveType Kind { get; private set; }
        public bool IsInterface { get; private set; }
        public bool IsISerializable { get; private set; }
        public bool IsReference { get; private set; }
        public bool IsSealed { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsEnum { get; private set; }
        public bool IsGeneric { get; private set; }
        public bool IsGenericParameter { get; private set; }
        public bool IsGenericTypeDefinition { get; private set; }
        public bool HasConverter { get; set; }
        public bool HasSurrogate { get; set; }

        public string FullName { get; private set; }
        public string Assembly { get; private set; }

        public TypeData BaseType { get; private set; }
        public TypeData Element { get; private set; }

        public RuntimeCollectionType CollectionType { get; private set; }
        public TypeData Collection1 { get; private set; }
        public TypeData Collection2 { get; private set; }

        public int ArrayRank { get; private set; }
        public int GenericParameterIndex { get; private set; }

        public IReadOnlyList<TypeData> GenericParameters { get; private set; }

        public MemberList<Member> Members { get; } = new MemberList<Member>();
        public class Member : IMember
        {
            internal Member() { }
            public string Name { get; internal set; }
            public TypeData Type { get; internal set; }
        }
    }
}
