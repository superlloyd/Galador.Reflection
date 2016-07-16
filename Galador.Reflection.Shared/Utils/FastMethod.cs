using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// Wrap a <see cref="MethodBase"/> and provide fast call with System.Emit when available,
    /// or fallback to normal reflection otherwise.
    /// </summary>
    public class FastMethod
    {
        ParameterInfo[] parameters;
        MethodBase method;
#if __NET__ || __NETCORE__
        MethodHandler fastMethod;
#else
#endif

        /// <summary>
        /// Name of the method.
        /// </summary>
        public string Name { get { return method.Name; } }

        /// <summary>
        /// Underlying method
        /// </summary>
        public MethodBase Method { get { return method; } }

        /// <summary>
        /// Construct a new <see cref="FastMethod"/> associated with an existing method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <remarks>the constructor is kept private as the FastMethod constructor itself is expensive and instance MUST be cached for performance improvement</remarks>
        internal FastMethod(MethodBase method)
        {
            parameters = method.GetParameters();
            this.method = method;
#if __NET__ || __NETCORE__
            fastMethod = EmitHelper.CreateMethodHandler(method);
#else
#endif
        }

        internal FastMethod(ConstructorInfo method, bool doNotCreate = false)
        {
            parameters = method.GetParameters();
            this.method = method;
#if __NET__ || __NETCORE__
            fastMethod = EmitHelper.CreateMethodHandler(method, doNotCreate);
#else
#endif
        }

        public object Invoke(object target, params object[] args)
        {
            if (args == null)
                args = Empty<object>.Array;
            if (args.Length < parameters.Length)
            {
                for (int i = args.Length; i < parameters.Length; i++)
                {
                    if (!parameters[i].HasDefaultValue)
                        throw new ArgumentException($"Method has {parameters.Length} parameters, only {args.Length} passed, argument {i} do NOT have a default value");
                }
                var args2 = new object[parameters.Length];
                for (int i = 0; i < args.Length; i++)
                    args2[i] = args[i];
                for (int i = args.Length; i < parameters.Length; i++)
                    args2[i] = parameters[i].DefaultValue;
                args = args2;
            }
#if __NET__ || __NETCORE__
            return fastMethod(target, args);
#else
            if (method is ConstructorInfo)
                return ((ConstructorInfo)method).Invoke(args);
            return method.Invoke(target, args);
#endif
        }
    }
}
