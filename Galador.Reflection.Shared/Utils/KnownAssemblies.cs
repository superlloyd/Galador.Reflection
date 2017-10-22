using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Galador.Reflection.Logging;

#if __NETCORE__
using Microsoft.Extensions.DependencyModel;
#endif

namespace Galador.Reflection.Utils
{
    /// <summary>
    /// A utility class to list all loaded assemblies and be notified when new one are loaded.
    /// </summary>
    public static class KnownAssemblies
    {
        static KnownAssemblies()
        {
#if __PCL__
#elif __NETCORE__
#else
            var domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad += (o, e) => AssemblyLoaded?.Invoke(e.LoadedAssembly);
#endif
        }

        static IEnumerable<Assembly> FilterOk(IEnumerable<Assembly> source)
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
                    TraceKeys.Traces[typeof(KnownAssemblies).FullName].Warning($"Couldn't get Types from {ass.GetName().Name})");
                    continue;
                }
                yield return ass;
            }
        }

        /// <summary>
        /// Enumerate all the currently loaded assembly.
        /// </summary>
        public static IEnumerable<Assembly> Current
        {
            get
            {
#if __PCL__
                throw new PlatformNotSupportedException("PCL");
#elif __NETCORE__
                Assembly LoadAssembly(string name)
                {
                    try { return Assembly.Load(new AssemblyName(name)); }
                    catch { return null; }
                }
                var compiled =
                    from lib in DependencyContext.Default.CompileLibraries
                    let ass = LoadAssembly(lib.Name)
                    where ass != null
                    select ass;
                return FilterOk(compiled);
#else
                var domain = AppDomain.CurrentDomain;
                return FilterOk(domain.GetAssemblies());
#endif
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
