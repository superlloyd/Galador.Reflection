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
                    TraceKeys.Serialization.Warning($"Couldn't get Type from {ass.GetName().Name})");
                    continue;
                }
                yield return ass;
            }
        }

        public static IEnumerable<Assembly> Current
        {
            get
            {
#if __PCL__
                throw new PlatformNotSupportedException("PCL");
#elif __NETCORE__
                var compiled =
                    from lib in DependencyContext.Default.CompileLibraries
                    let ass = Assembly.Load(new AssemblyName(lib.Name))
                    select ass;
                return FilterOk(compiled);
#else
                var domain = AppDomain.CurrentDomain;
                return FilterOk(domain.GetAssemblies());
#endif
            }
        }

        public static event Action<Assembly> AssemblyLoaded;
    }
}
