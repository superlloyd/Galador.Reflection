using Galador.Reflection.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var tSer = new SerializationTests();
            tSer.CheckIsFastEnough();

            //KnownTypes.Register(typeof(SerializationTests).Assembly);
            //var csharp = ObjectContext.GenerateCSharpCode("Generated"
            //    , typeof(SerializationTests.BigClass)
            //    , typeof(SerializationTests.SimpleClass<Dictionary<int, string>>)
            //    , typeof(SerializationTests.BList)
            //    , typeof(SerializationTests.Serial2)
            //    );

            //var t1 = typeof(List<>);
            //var t2 = t1.GetGenericTypeDefinition();
            //var s1 = t1.FullName;
            //var tSer = new SerializationTests();
            //tSer.WinFormTest();
        }

    }
}
