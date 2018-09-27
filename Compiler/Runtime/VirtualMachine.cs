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
        public void Run(string Text, int OptimizeLevel)
        {
            InstructionSet = Generator.GenerateCode(Text, OptimizeLevel);
            if (InstructionSet == null)
            {
                Generator.PrintError();
                Console.WriteLine("Please correct all errors before running code");
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
                    Console.WriteLine(e.Message);
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
                case PCode.ADD:
                    Push(Pop() + Pop());
                    break;
                case PCode.CAL:
                    EIP = cmd.Arg;
                    Push(EIP + 1);
                    return;
                case PCode.DIV:
                    int tmp = Pop();
                    Push(Pop() / tmp);
                    break;
                case PCode.EQL:
                    PushBoolean(Pop() == Pop());
                    break;
                case PCode.EXP:
                    TempPool.Clear();
                    EIP = Pop();
                    break;
                case PCode.GEQ:
                    PushBoolean(Pop() <= Pop());
                    break;
                case PCode.GRT:
                    PushBoolean(Pop() < Pop());
                    break;
                case PCode.HALT:
                    throw new Exception();
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
                case PCode.LER:
                    PushBoolean(Pop() >= Pop());
                    break;
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
                case PCode.LSS:
                    PushBoolean(Pop() > Pop());
                    break;
                case PCode.MOD:
                    tmp = Pop();
                    Push(Pop() % tmp);
                    break;
                case PCode.MUL:
                    Push(Pop() * Pop());
                    break;
                case PCode.NEQ:
                    PushBoolean(Pop() != Pop());
                    break;
                case PCode.RED:
                    string i;
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
                        Console.WriteLine("Please input a legal INT");
                        i = Console.ReadLine();
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
                case PCode.SUB:
                    tmp = Pop();
                    Push(Pop() - tmp);
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
                case PCode.XOR:
                    Push(Pop() ^ Pop());
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

        private void SetInOutFunction(ReadDelegate ReadFunction = null, WriteDelegate WriteFunction = null)
        {
            Read = ReadFunction;
            Write = WriteFunction;
        }
        private ReadDelegate Read = null;
        private WriteDelegate Write = null;
        public delegate string ReadDelegate();
        public delegate void WriteDelegate(int v);
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
