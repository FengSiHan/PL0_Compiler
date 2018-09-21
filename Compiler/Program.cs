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
            string code = File.ReadAllText(@"C:\Users\FSH\source\repos\Compiler\Compiler\test.pl0");
            Compiler compiler = new Compiler();
            compiler.Parse(code);
            compiler.PrintError();
            compiler.GenerateCode();
            Console.WriteLine("输入任意键结束...");
            Console.ReadKey();
        }
    }
}