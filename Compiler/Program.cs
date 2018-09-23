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
            ILGenerator compiler = new ILGenerator();
            compiler.GenerateCode(code, 1);
            compiler.PrintQCode();
            Console.WriteLine("输入任意键结束...");
            Console.ReadKey();
        }
    }
}