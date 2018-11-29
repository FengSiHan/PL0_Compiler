using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Compiler
{
    public class VirtualMachine
    {
        /// <summary>
        /// 执行输入的代码
        /// </summary>
        /// <param name="Text">待执行的代码</param>
        /// <param name="OptimizeLevel">优化等级0 ~ 3</param>
        public void Run(string Text, int OptimizeLevel = 0)
        {
            InstructionSet = Generator.GenerateCode(Text, OptimizeLevel);
            if (Generator.NumOfError > 0)
            {
                return;
            }
            if (InstructionSet == null || InstructionSet.Count < 2)
            {
                foreach (var i in Generator.ErrorMsg.Errors)
                {
                    if (Write != null)
                    {
                        WriteString(i.ToString());
                    }
                    else
                    {
                        Console.WriteLine(i.ToString());
                    }
                }
                return;
            }
            Reset();
            while (EIP != InstructionSet.Count - 1)
            {
                try
                {
                    Execute(InstructionSet[EIP]);
                }
                catch (Exception e)
                {
                    if (Write != null)
                    {
                        WriteString(e.Message);
                    }
                    else
                    {
                        Console.WriteLine(e.Message);
                    }
                    return;
                }
            }
        }

        public VirtualMachine()
        {
            RuntimeStack = new Stack<int>();
            TempPool = new Dictionary<int, int>();
            Generator = new PCodeGeneraotr();
        }

        private void Execute(PNode cmd)
        {
            switch (cmd.INS)
            {
                case PCode.OPR:
                    switch (cmd.Arg)
                    {
                        case 1:
                            Push(-Pop());
                            break;
                        case 2:
                            Push(Pop() + Pop());
                            break;
                        case 3:
                            int tmp1 = Pop();
                            Push(Pop() - tmp1);
                            break;
                        case 4:
                            break;
                        case 5:
                            tmp1 = Pop();
                            Push(Pop() / tmp1);
                            break;
                        case 6:
                            Push(Pop() & 1);
                            break;
                        case 8:
                            PushBoolean(Pop() == Pop());
                            break;
                        case 9:
                            PushBoolean(Pop() != Pop());
                            break;
                        case 10:
                            PushBoolean(Pop() > Pop());
                            break;
                        case 11:
                            PushBoolean(Pop() <= Pop());
                            break;
                        case 12:
                            PushBoolean(Pop() < Pop());
                            break;
                        case 13:
                            PushBoolean(Pop() >= Pop());
                            break;
                    }
                    break;
                case PCode.CAL:
                    Push(EIP + 1);
                    EIP = cmd.Arg;
                    return;
                case PCode.EXP:
                    TempPool.Clear();
                    EIP = Pop();
                    return;
                case PCode.HALT:
                    return;
                case PCode.INT:
                    Push(Pop() + cmd.Arg);
                    break;
                case PCode.JMP:
                    EIP = cmd.Arg;
                    TempPool.Clear();
                    return;
                case PCode.JPC:
                    if (Pop() == 1)
                    {
                        EIP++;
                    }
                    else
                    {
                        EIP = cmd.Arg;
                    }
                    TempPool.Clear();
                    return;
                case PCode.LIT:
                    Push(cmd.Arg);
                    break;
                case PCode.LOD:
                    if (cmd.DataType == 3)
                    {
                        if (Initialized[cmd.Arg] == false)
                        {
                            throw new Exception($"使用了未初始化的变量{VarSeg[cmd.Arg].Value}");
                        }
                        Push(DataSegment[cmd.Arg]);
                    }
                    else
                    {
                        Push(TempPool[cmd.Arg]);
                    }
                    break;
                case PCode.MOD:
                    int tmp = Pop();
                    Push(Pop() % tmp);
                    break;
                case PCode.RED:
                    string i;
                    if (Write == null)
                    {
                        Console.WriteLine($"Please input a value for {VarSeg[cmd.Arg].Value}");
                    }
                    else
                    {
                        //Write($"Please input a value for {VarSeg[cmd.Arg].Value}");
                    }
                    if (Read == null)
                    {
                        i = Console.ReadLine();
                    }
                    else
                    {
                        i = Read();
                    }
                    int res;
                    while (!int.TryParse(i, out res))
                    {
                        if (WriteString != null)
                        {
                            WriteString("Please input a legal INT");
                        }
                        else
                        {
                            Console.WriteLine("Please input a legal INT");
                        }
                        if (Read != null)
                        {
                            i = Read();
                        }
                        else
                        {
                            i = Console.ReadLine();
                        }
                    }
                    Initialized[cmd.Arg] = true;
                    DataSegment[cmd.Arg] = res;
                    break;
                case PCode.STO:
                    if (cmd.DataType == 3)
                    {
                        Initialized[cmd.Arg] = true;
                        DataSegment[cmd.Arg] = Pop();
                    }
                    else
                    {
                        if (TempPool.ContainsKey(cmd.Arg))
                        {
                            TempPool[cmd.Arg] = Pop();
                        }
                        else
                        {
                            TempPool.Add(cmd.Arg, Pop());
                        }
                    }
                    break;
                case PCode.WRT:
                    if (Write == null)
                    {
                        Console.WriteLine(Pop());
                    }
                    else
                    {
                        Write(Pop());
                    }
                    break;
                case PCode.NOT:
                    Push(Pop() ^ 1);
                    break;
            }
            ++EIP;
        }

        private void PushBoolean(bool value)
        {
            Push(value ? 1 : 0);
        }

        private int Pop()
        {
            return RuntimeStack.Pop();
        }

        private void Push(int r)
        {
            RuntimeStack.Push(r);
        }

        private void Reset()
        {
            EIP = 0;
            TempPool.Clear();
            RuntimeStack.Clear();
            VarSeg = Generator.VarSeg;
            DataSegment = new int[Generator.VarSeg.Count];
            Initialized = new bool[Generator.VarSeg.Count];
            for (int i = 0; i < DataSegment.Length; ++i)
            {
                DataSegment[i] = 0;
                Initialized[i] = false;
            }
        }

        public void SetInOutFunction(ReadDelegate ReadFunction = null, WriteDelegate WriteFunction = null, WriteStringDelegate WriteStringFunc = null)
        {
            Read = ReadFunction;
            Write = WriteFunction;
            WriteString = WriteStringFunc;
        }
        private ReadDelegate Read = null;
        private WriteDelegate Write = null;
        private WriteStringDelegate WriteString = null;
        public delegate string ReadDelegate();
        public delegate void WriteDelegate(int v);
        public delegate void WriteStringDelegate(string v);
        private Stack<int> RuntimeStack;
        private Dictionary<int, int> TempPool;
        private List<QuadrupleNode> VarSeg;
        private int EIP;
        private List<PNode> InstructionSet;
        private int[] DataSegment;
        private bool[] Initialized;
        private PCodeGeneraotr Generator;
    }
}
