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
        /// <summary>
        /// On completion of the deserialization process this method will be called.
        /// </summary>
        void Deserialized();
    }
}
