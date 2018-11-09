using System;
using System.Collections.Generic;
using System.Text;
using SRS = System.Runtime.Serialization;

namespace Galador.Reflection.Serialization
{
    public class ObjectData
    {
        internal ObjectData(TypeData type)
        {
            TypeData = type;
        }

        public TypeData TypeData { get; }

        public SRS.SerializationInfo Info { get; internal set; }
        public string ConverterString { get; internal set; }
        public object SurrogateObject { get; internal set; }
    }
}
