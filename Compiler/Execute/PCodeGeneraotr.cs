using System;
using System.Collections;
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
            Programs = new List<PNode>();
        }
        public void GeneratePCode(string Text, int Level)
        {
            GetIL.GenerateCode(Text, Level);
            if (GetIL.NumOfError > 0)
            {
                GetIL.PrintError();
                Console.WriteLine("Please correct all errors before generaing code");
                return;
            }
            GetIL.GetCode(ref CodeSeg, ref VarSeg);
            GetPCode();
        }
        public void PrintCode()
        {
            if (Programs.Count == 0)
            {
                Console.WriteLine("Please Call 'GeneratePCode first'");
                return;
            }
            int index = 0;
            foreach (var i in Programs)
            {
                Console.Write(string.Format("{0,-3}-> ", index++));
                Console.WriteLine(string.Format("{0,-6} ", Enum.GetName(i.INS.GetType(), i.INS)));
            }
        }
        private void GetPCode()
        {
            Programs.Clear();
            foreach (var i in CodeSeg)
            {
                Translate(i);
            }
            Programs[Programs.Count - 1] = new PNode(PCode.HALT);
        }
        private void Translate(QuadrupleNode Node)
        {
            int typeV = Convert.ToInt32(Node.Type);
            if (typeV <= IsJump)
            {
                TranslateJump(Node);
                return;
            }
            switch (Node.Type)
            {
                case QuadrupleType.Return:
                    Add(new PNode(PCode.EXP));
                    break;
                case QuadrupleType.Call:
                    Add(new PNode(PCode.CAL, (int)Node.Result, 4));
                    break;
                case QuadrupleType.Add:
                case QuadrupleType.Sub:
                case QuadrupleType.Mul:
                case QuadrupleType.Div:
                    TranslateOpr(Node);
                    break;
                case QuadrupleType.Assign:
                    Add(new PNode(PCode.STO, ((QuadrupleNode)(Node.Arg1)).Offset, 3));
                    break;
                case QuadrupleType.Write:
                    var list = Node.Arg1 as ArrayList;
                    foreach (var i in list)
                    {
                        var param = i as QuadrupleNode;
                        Add(new PNode(PCode.LOD, param.Offset, 3));
                        Add(new PNode(PCode.WRT));
                    }
                    break;
                case QuadrupleType.Read:
                    list = Node.Arg1 as ArrayList;
                    foreach (var i in list)
                    {
                        var param = i as QuadrupleNode;
                        Add(new PNode(PCode.RED, param.Offset, 3));
                    }
                    break;
            }

        }
        private void TranslateJump(QuadrupleNode Node)
        {
            if (Node.Type == QuadrupleType.JMP)
            {
                Add(new PNode(PCode.JMP, (int)Node.Result, 4));
                return;
            }
            if (Node.Type == QuadrupleType.JO || Node.Type == QuadrupleType.JNO)
            {
                if (Node.Arg1 is int)
                {
                    Add(new PNode(PCode.LOD, (int)Node.Arg1, 2));
                }
                else if (Node.Arg1 is string)
                {
                    int arg = int.Parse(((string)Node.Arg1).Substring(1));
                    Add(new PNode(PCode.LIT, arg, 1));
                }
                else
                {
                    var t = Node.Arg1 as QuadrupleNode;
                    Add(new PNode(PCode.STO, t.Offset, 3));
                }
                Add(new PNode(PCode.LIT, 2, 1));
                Add(new PNode(PCode.MOD));
                if (Node.Type == QuadrupleType.JO)
                {
                    Add(new PNode(PCode.LIT, 1, 1));
                    Add(new PNode(PCode.XOR));
                }
                Add(new PNode(PCode.JPC));
                return;
            }
            LoadArg(Node);
            switch (Node.Type)//根据条件选择相反的指令，因为是if False跳转
            {
                case QuadrupleType.JE:
                    Add(new PNode(PCode.NEQ));
                    break;
                case QuadrupleType.JG:
                    Add(new PNode(PCode.LER));
                    break;
                case QuadrupleType.JGE:
                    Add(new PNode(PCode.LSS));
                    break;
                case QuadrupleType.JL:
                    Add(new PNode(PCode.GRT));
                    break;
                case QuadrupleType.JLE:
                    Add(new PNode(PCode.GRT));
                    break;
                case QuadrupleType.JNE:
                    Add(new PNode(PCode.EQL));
                    break;
            }
            Add(new PNode(PCode.JPC));
        }
        private void TranslateOpr(QuadrupleNode Node)
        {
            LoadArg(Node);
            switch (Node.Type)
            {
                case QuadrupleType.Add:
                    Add(new PNode(PCode.ADD));
                    break;
                case QuadrupleType.Sub:
                    Add(new PNode(PCode.SUB));
                    break;
                case QuadrupleType.Mul:
                    Add(new PNode(PCode.MUL));
                    break;
                case QuadrupleType.Div:
                    Add(new PNode(PCode.DIV));
                    break;
            }
        }
        private void LoadArg(QuadrupleNode Node)
        {
            if (Node.Arg1 is int)
            {
                Add(new PNode(PCode.LOD, (int)Node.Arg1, 2));
            }
            else if (Node.Arg1 is string)
            {
                int arg = int.Parse(((string)Node.Arg1).Substring(1));
                Add(new PNode(PCode.LIT, arg, 1));
            }
            else
            {
                var t = Node.Arg1 as QuadrupleNode;
                Add(new PNode(PCode.STO, t.Offset, 3));
            }
            if (Node.Arg2 is int)
            {
                Add(new PNode(PCode.LOD, (int)Node.Arg2, 2));
            }
            else if (Node.Arg2 is string)
            {
                int arg = int.Parse(((string)Node.Arg2).Substring(1));
                Add(new PNode(PCode.LIT, arg, 1));
            }
            else
            {
                var t = Node.Arg2 as QuadrupleNode;
                Add(new PNode(PCode.STO, t.Offset, 3));
            }
        }
        private void Add(PNode node)
        {
            Programs.Add(node);
        }
        private List<PNode> Programs;
        private ILGenerator GetIL;
        private List<QuadrupleNode> VarSeg, CodeSeg;
        private int IsJump = Convert.ToInt32(QuadrupleType.JMP);
    }


    enum PCode
    {
        //lit 0, a : load constant a    读取常量a到数据栈栈顶
        //opr 0, a : execute operation a    执行a运算
        //lod l, a : load variable l, a    读取变量放到数据栈栈顶，变量的相对地址为a，层次差为1
        //sto l, a : store variable l, a    将数据栈栈顶内容存入变量，变量的相对地址为a，层次差为1
        //cal l, a : call procedure a at level l    调用过程，过程入口指令为a, 层次差为1
        //int 0, a : increment t-register by a    数据栈栈顶指针增加a
        //jmp 0, a : jump to a    无条件跳转到指令地址a
        //jpc 0, a : jump conditional to a    条件转移到指令地址a
        //red l, a : read variable l, a    读数据并存入变量，
        //wrt 0, 0 : write stack-top    将栈顶内容输出
        LIT,
        LOD,
        STO,
        CAL,
        INT,
        JMP,
        JPC,
        RED,
        WRT,
        HALT,
        EXP,
        SUB,
        ADD,
        MUL,
        DIV,
        MOD,
        EQL,//=
        NEQ,//<>
        LSS,//<
        LER,//<=
        GRT,//>
        GEQ,//>=
        XOR //^
    }
    class PNode
    {
        internal readonly PCode INS;

        internal readonly int Arg;

        internal readonly int DataType;

        /// <summary>
        /// Type: 1:立即数，2:临时变量，3:变量，4:地址
        /// </summary>
        /// <param name="ins"></param>
        /// <param name="arg"></param>
        /// <param name="type"> </param>
        internal PNode(PCode ins, int arg, int type)
        {
            DataType = type;
            Arg = arg;
            INS = ins;
        }
        internal PNode(PCode ins)
        {
            INS = ins;
        }
    }


}
