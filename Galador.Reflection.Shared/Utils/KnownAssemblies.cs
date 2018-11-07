using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// A utility class to list all loaded assemblies and be notified when new one are loaded.
    /// </summary>
    public static class KnownAssemblies
    {
        static KnownAssemblies()
        {
            var domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad += (o, e) => AssemblyLoaded?.Invoke(e.LoadedAssembly);
        }

        /// <summary>
        /// Enumerate all the currently loaded assembly.
        /// </summary>
        public static IEnumerable<Assembly> Current
        {
            get
            {
                var domain = AppDomain.CurrentDomain;
                return FilterOk(domain.GetAssemblies());

                IEnumerable<Assembly> FilterOk(IEnumerable<Assembly> source)
                {
                    if (source == null)
                        yield break;
                    foreach (var ass in source)
                    {
                        if (ass == null)
                            continue;
                        try
                        {
                            var dt = ass.DefinedTypes;
                        }
                        catch
                        {
                            Log.Warning(typeof(KnownAssemblies).FullName, $"Couldn't get Types from {ass.GetName().Name})");
                            continue;
                        }
                        yield return ass;
                    }
                }
            }
        }

#pragma warning disable 67
        /// <summary>
        /// Occurs when an assembly is loaded.
        /// </summary>
        public static event Action<Assembly> AssemblyLoaded;
#pragma warning restore 67

        /// <summary>
        /// Lookup a type by type name and assembly name.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <returns>A type or null.</returns>
        public static Type GetType(string typeName, string assemblyName)
        {
            if (assemblyName == null)
                return Type.GetType(typeName);

            var candidates =
                from ass in Current
                where ass.GetName().Name == assemblyName
                let t = ass.GetType(typeName)
                where t != null
                select t
                ;
            return candidates.FirstOrDefault();
        }
    }
}
