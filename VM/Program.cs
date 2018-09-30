using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compiler;
namespace VM
{
    public class Program
    {
        public static void Main(string[] arg)
        {
            VirtualMachine vm = new VirtualMachine();
            vm.Run(arg[0], Convert.ToInt32(arg[1]));
            Console.WriteLine("请按任意键继续...");
            Console.ReadKey();
        }
    }
}
