using System;
using System.Reflection;
using TestApp;
using Xunit;
using System.Linq;

namespace TestNETCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //KnownTypes.RegisterAssemblies(typeof(ReflectType2).GetTypeInfo().Assembly);
            //KnownTypes.RegisterAssemblies(typeof(Program).GetTypeInfo().Assembly);

            //RunSomeTests();
            RunAllTests();

            Console.WriteLine();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        public static void RunSomeTests()
        {
            //var tReg = new RegistryTests();
            //tReg.Create1();

            var tSer = new SerializationTests();
            //tSer.CheckIsSmall();
            //tSer.CheckGeneric();

            tSer.CheckComplexClass();
            tSer.CheckSimpleTypes();
        }

        public static void RunAllTests()
        {
            RunTests(typeof(RegistryTests), typeof(PathTests), typeof(SerializationTests));
        }
        public static void RunTests(params Type[] testTypes)
        {
            var empty = new object[0];
            foreach (var t in testTypes)
            {
                Console.WriteLine($"Now Testing {t.FullName}:");
                Console.WriteLine("==================");
                var methods = from m in t.GetTypeInfo().GetMethods()
                              let fa = m.GetCustomAttribute<FactAttribute>()
                              where fa != null
                              select m;
                var tc = Activator.CreateInstance(t);
                foreach (var m in methods)
                {
                    Console.Write($"\tTest {m.Name}....");
                    try
                    {
                        m.Invoke(tc, empty);
                        Console.WriteLine("\tSuccess");
                    }
                    catch (Exception ex)
                    {
                        var exb = ex.GetBaseException();
                        Console.WriteLine($"\tError ({exb.Message ?? exb.GetType().Name})");
                    }
                }
                Console.WriteLine();
            }
        }
    }
}