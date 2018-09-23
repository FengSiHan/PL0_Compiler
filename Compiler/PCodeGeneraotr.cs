using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    class PCodeGeneraotr
    {
        public PCodeGeneraotr()
        {
            GetIL = new ILGenerator();
        }
        public void GeneratePcode(string Text, int Level)
        {
            GetIL.GenerateCode(Text, Level);
            if (GetIL.NumOfError > 0)
            {
                GetIL.PrintError();
                Console.WriteLine("Please correct all errors before generaing code");
                return;
            }
            GetIL.GetCode(ref CodeSeg, ref VarSeg);
        }
        private ILGenerator GetIL;
        private List<QuadrupleNode> VarSeg, CodeSeg;
    }
}
