using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Galador.Reflection.Utils
{
    public class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }

        public bool Equals(T left, T right)
        {
            return Object.ReferenceEquals(left, right);
        }
    }

    public class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public int GetHashCode(object value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }

        bool IEqualityComparer<object>.Equals(object left, object right)
        {
            return Object.ReferenceEquals(left, right);
        }
    }
}
