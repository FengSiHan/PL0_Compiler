﻿using System;
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
            Parser parser = new Parser();
            parser.Parse(code);
            parser.PrintErrorMsg();
            if (parser.ErrorMsg.Count() != 0)
            {
                Console.WriteLine("按任意键继续");
                Console.ReadKey();
            }
            ILGenerator ilg = new ILGenerator();
            ilg.GenerateCode(code, 0);
            ilg.PrintCode();
            Console.WriteLine("按任意键继续");
            Console.ReadKey();

            PCodeGeneraotr pcg = new PCodeGeneraotr();
            pcg.GenerateCode(code, 0);
            pcg.PrintCode();
            Console.WriteLine("按任意键开始执行");
            Console.ReadKey();


            VirtualMachine vm = new VirtualMachine();
            vm.Run(code, 0);
        }
    }
}