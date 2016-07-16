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

            //tSer.CheckWriteIsFastEnough();
            //tSer.CheckReadIsFastEnough();

            var csharp = ObjectContext.GenerateCSharpCode("Generated"
                , typeof(SerializationTests.BigClass)
                , typeof(SerializationTests.SimpleClass<Dictionary<int, string>>)
                , typeof(SerializationTests.BList)
                , typeof(SerializationTests.Serial2)
                , typeof(SerializationTests.Generic01<int, string>)
                );

            tSer.WinFormTest();
        }

    }
}
