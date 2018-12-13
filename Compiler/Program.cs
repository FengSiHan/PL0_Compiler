using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{

    class Program
    {
        public static void Main(string[] arg)
        {
            string code = File.ReadAllText($"../../test.pl0");
            Lexer lexer = new Lexer(code);
            foreach (var i in lexer.Scan())
            {
                Console.WriteLine(string.Format("{0, -10}", i.Content) + "   " + i.Location);
            }
            Console.WriteLine("\n四元式:");
            Parser parser = new Parser();
            parser.Parse(code);
            Console.WriteLine(parser.GetErrorMsgString());

            ILGenerator ilg = new ILGenerator();
            ilg.GenerateCode(code, 0);
            Console.WriteLine(ilg.GetCodeString());
            Console.WriteLine("\nPCode\n");

            PCodeGeneraotr pcg = new PCodeGeneraotr();
            pcg.GenerateCode(code, 0);
            Console.WriteLine(pcg.GetPCodeString());
            Console.WriteLine("按任意键开始执行");
            Console.ReadKey();

            VirtualMachine vm = new VirtualMachine();
            vm.Run(code, 0);
        }
    }
}