using System;
using System.Collections.Generic;
using System.Text;

namespace Galador.Reflection.Serialization
{
    /// <summary>
    /// This method will be called on deserialized object for further deserialization 
    /// post processing. It will be called immediately on value type and when the whole
    /// object tree has been process on value type.
    /// </summary>
    public interface IDeserialized
    {
        void Deserialized();
    }
}
