﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    /// <summary>
    /// 中间代码生成器
    /// </summary>
    public class ILGenerator
    {
        public string GetErrorMsgString()
        {
            return parser.GetErrorMsgString();
        }

        /// <summary>
        /// 产生中间代码（可以选择优化等级）
        /// </summary>
        /// <param name="Text">优化代码</param>
        /// <param name="Level">优化等级为0 ~ 3</param>
        public void GenerateCode(string Text, int Level)
        {
            Parse(Text);
            Done = true;
            OptimizationLevel = Level;
            if (NumOfError != 0)
            {
                return;
            }
            Clear();
            GetQuadruples(Root, 0);
            LocateJumpNodeAndDetermineNodeOffset();//获取四元式后，回填跳转地址并且对每个节点赋于地址值
            Optimize optimize = new Optimize(CodeSeg, VarSeg, CodeEntrance);
            if (Level > 0)
            {
                optimize.LocalOptimization();
            }
            if (Level > 1)
            {
                optimize.LoopOptimization();
            }
            if (Level > 2)
            {
                optimize.GlobalOptimization();//O(n^4警告)
            }
            if (Level > 3)
            {
                Console.WriteLine("4级别优化请给Jeff Dean发邮件");
            }
            if (Level > 0)
            {
                CodeSeg = optimize.GenerateCode();
            }
            else
            {
                //添加程序入口
                List<QuadrupleNode> Code = new List<QuadrupleNode>();
                Code.Add(new QuadrupleNode(QuadrupleType.JMP) { Result = CodeEntrance + 1 });
                int JumpValue = Convert.ToInt32(QuadrupleType.JMP);
                foreach (var i in CodeSeg)
                {
                    QuadrupleType type = i.Type;
                    int v = Convert.ToInt32(type);
                    if (v <= JumpValue || type == QuadrupleType.Call)
                    {
                        i.Result++;
                    }
                    Code.Add(i);
                }
                CodeSeg = Code;
            }
        }

        public string GetCodeString()
        {
            StringBuilder sb = new StringBuilder();
            if (!Done)
            {
                sb.Append("Please generate intermediate code first\n");
                return sb.ToString();
            }
            if (NumOfError != 0)
            {
                sb.Append("Code needs correcting before generating code\n");
                return sb.ToString();
            }
            int index = 0;
            foreach (QuadrupleNode i in CodeSeg)
            {
                if (i == null)
                {
                    continue;
                }
                sb.Append(string.Format("{0,-3}-> ", index++));
                sb.Append(string.Format("{0,-6} ", Enum.GetName(i.Type.GetType(), i.Type)));
                if (i.Type == QuadrupleType.Return)
                {
                    sb.Append("\n");
                    continue;
                }
                else if (i.Type == QuadrupleType.JMP)
                {
                    sb.Append(i.Result);
                    sb.Append("\n");
                    continue;
                }
                if (i.Type == QuadrupleType.Write || i.Type == QuadrupleType.Read)
                {
                    ArrayList list = (ArrayList)i.Arg1;
                    for (int k = 0; k < list.Count; ++k)
                    {
                        if (list[k] is int)
                        {
                            sb.Append($"t{list[k]}");
                        }
                        else if (list[k] is QuadrupleNode)
                        {
                            var t = list[k] as QuadrupleNode;
                            if (t.Type == QuadrupleType.Var)
                            {
                                sb.Append(VarSeg[t.Offset].Value);
                            }
                        }
                        else
                        {
                            sb.Append(list[k]);
                        }
                        if (k != list.Count - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                    sb.Append('\n');
                    continue;
                }
                else if (i.Type == QuadrupleType.Call)
                {
                    sb.Append(i.Result);
                    sb.Append('\n');
                    continue;
                }
                else if (i.Arg1 is QuadrupleNode)
                {
                    var t = i.Arg1 as QuadrupleNode;
                    if (t.Type == QuadrupleType.Var)
                    {
                        sb.Append(t.Value);
                    }
                    else sb.Append($"t{t.Result}");
                }
                else if (i.Arg1 is string && ((string)i.Arg1).StartsWith("#"))
                {
                    sb.Append(i.Arg1);
                }
                else if (i.Arg1 != null)
                {
                    sb.Append($"t{i.Arg1}");
                }
                sb.Append(", ");
                if (i.Arg2 is QuadrupleNode)
                {
                    var t = i.Arg2 as QuadrupleNode;
                    if (t.Type == QuadrupleType.Var)
                    {
                        sb.Append(t.Value);
                    }
                    else
                    {
                        sb.Append($"t{t.Result}");
                    }
                }
                else if (i.Arg2 is string && ((string)i.Arg2).StartsWith("#"))
                {
                    sb.Append(i.Arg2);
                }
                else if (i.Arg2 != null)
                {
                    sb.Append($"t{i.Arg2}");
                }
                if (Enum.GetName(i.Type.GetType(), i.Type).StartsWith("J") == false && i.Result != null)
                {
                    int value = (int)i.Result;
                    if (value < 0)
                    {
                        sb.Append($", {VarSeg[-value].Value}");
                    }
                    else
                    {
                        sb.Append($", t{i.Result}");
                    }
                }
                else if (i.Result != null)
                {
                    if (i.Result > 0)
                    {
                        sb.Append(", " + i.Result);
                    }
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public ILGenerator()
        {
            ConstSeg = new List<int>(16);
            ProcedureSeg = new List<QuadrupleNode>(16);
            VarSeg = new List<QuadrupleNode>(16);
            CodeSeg = new List<QuadrupleNode>(64);
            FreeDataIndex = 0;
            NumOfError = 0;
            CodeEntrance = -1;
            OptimizationLevel = 0;
            Done = false;
        }

        #region Uitls&Members

        /// <summary>
        /// 0~3等级
        /// </summary>
        private int OptimizationLevel;

        /// <summary>
        /// 指示编译完成
        /// </summary>
        private bool Done;

        internal void GetInfo(ref List<QuadrupleNode> Code, ref List<QuadrupleNode> Var)
        {
            if (!Done)
            {
                Console.WriteLine("Please generate intermediate code first");
                return;
            }
            Code = CodeSeg;
            Var = VarSeg;
        }

        private void Parse(string text)
        {
            parser = new Parser();
            Root = parser.Parse(text);
            NumOfError = parser.GetNumofErrors();
            ErrorMsg = parser.ErrorMsg;
        }

        public int NumOfError { get; private set; }

        internal void ChangeCodeSeg(List<QuadrupleNode> list)
        {
            CodeSeg = list;
        }

        private void Clear()
        {
            VarSeg.Clear();
            ConstSeg.Clear();
            CodeSeg.Clear();
        }

        private int GetTemp()
        {
            if (FreeDataIndex == MaxTempDataNum)
            {
                Console.WriteLine("不用看了程序崩了，需要2333个临时变量认真的？");
                return MaxTempDataNum;
            }
            return FreeDataIndex++;
        }

        private void FreeAllTempData() //保证一个基本块内不会执行,即保证基本块内临时变量不重复
        {
            FreeDataIndex = 0;
        }

        private Object GetArg(AstNode node, int level)
        {
            if (node.Type == AstType.NUM)
            {
                return "#" + node.Info;
            }
            else if (node.Type == AstType.Var)
            {
                return new QuadrupleNode(VarSeg[node.Offset], level);
            }
            else if (node.Type == AstType.Const)
            {
                return "#" + node.Right.Info;
            }
            else
            {
                return null;
            }
        }

        private void GetQuadruples(AstNode now, int Level)//dfs
        {
            if (now == null)
            {
                return;
            }
            switch (now.Type)
            {
                case AstType.SubProgram:
                    GetQuadruples(now.Left, Level);
                    if (CodeEntrance == -1)//为1尚未被赋值
                    {
                        if (now.Right == null)//没有起始语句
                        {
                            CodeEntrance = -2;
                        }
                        else
                        {
                            CodeEntrance = CodeSeg.Count;
                        }
                    }
                    GetQuadruples(now.Right, Level);
                    if (now.Right != null)
                    {
                        CodeSeg.Add(new QuadrupleNode(QuadrupleType.Return));
                    }
                    break;
                case AstType.Define:
                    GetQuadruples(now.Left, Level);
                    GetQuadruples(now.Right, Level + 1);
                    break;
                case AstType.IdDefine:
                    GetQuadruples(now.Left, Level);
                    GetQuadruples(now.Right, Level);
                    break;
                case AstType.ConstDefine:
                    List<AstNode> list = now.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    foreach (var i in list)
                    {
                        i.Offset = ConstSeg.Count;
                        ConstSeg.Add(Convert.ToInt32(i.Right.Info));
                    }
                    break;
                case AstType.VarDefine:
                    list = now.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    int cnt = 0;
                    foreach (var i in list)
                    {
                        i.Offset = VarSeg.Count;
                        QuadrupleNode var = new QuadrupleNode(QuadrupleType.Var)
                        {
                            Value = i.Left.Info,
                            Offset = i.Offset,
                            AddressOffset = cnt++,
                            Level = Level
                        };
                        VarSeg.Add(var);
                    }
                    break;
                case AstType.ProcsDefine:
                    list = now.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    foreach (var i in list)
                    {
                        i.Offset = ProcedureSeg.Count;
                        var proc = new QuadrupleNode(QuadrupleType.Proc)
                        {
                            Value = i.Left.Info,
                            Level = Level
                        };
                        ProcedureSeg.Add(proc);
                        GetQuadruples(i.Right.Left, Level);
                        proc.Offset = CodeSeg.Count;//调用直接跳转到当前行
                        GetQuadruples(i.Right.Right, Level);
                        CodeSeg.Add(new QuadrupleNode(QuadrupleType.Return));
                        FreeAllTempData();
                    }
                    break;
                case AstType.Statements:
                    list = now.Info as List<AstNode>;
                    foreach (var i in list)
                    {
                        GetQuadruples(i, Level);
                    }
                    break;
                case AstType.Assign:
                    QuadrupleNode assign = new QuadrupleNode(QuadrupleType.Assign, new QuadrupleNode(VarSeg[now.Left.Offset], Level))
                    {
                        Arg2 = GetArg(now.Right, Level)
                    };
                    if (assign.Arg2 == null)
                    {
                        GetQuadruples(now.Right, Level);
                        assign.Arg2 = ResultIndex;
                    }
                    CodeSeg.Add(assign);
                    break;
                case AstType.Call:
                    CodeSeg.Add(new QuadrupleNode(QuadrupleType.Call)
                    {
                        Result = ProcedureSeg[now.Left.Offset].Offset//跳转地址
                    });
                    break;
                case AstType.IfElse:
                    QuadrupleNode node = new QuadrupleNode(GetOppositeJumpInstruction(now.Left.Left.Info))
                    {
                        Arg1 = GetArg(now.Left.Left.Left, Level)
                    };
                    if (node.Arg1 == null)
                    {
                        GetQuadruples(now.Left.Left.Left, Level);//左边表达式
                        node.Arg1 = ResultIndex;
                    }
                    if (node.Type != QuadrupleType.JO && node.Type != QuadrupleType.JNO)
                    {
                        node.Arg2 = GetArg(now.Left.Left.Right, Level);
                        if (node.Arg2 == null)
                        {
                            GetQuadruples(now.Left.Left.Right, Level);
                            node.Arg2 = ResultIndex;
                        }
                    }

                    CodeSeg.Add(node);
                    FreeAllTempData();
                    GetQuadruples(now.Left.Right, Level);
                    node.Result = CodeSeg.Count;
                    //不成立跳转到隶属then的语句的之后
                    //跳转指令的result(即目标地址)需要指向一个节点
                    //扫描一遍回填
                    if (now.Right != null)//有else
                    {
                        //if x then y else z ; t
                        QuadrupleNode LeaveIf = new QuadrupleNode(QuadrupleType.JMP);//goto t
                        CodeSeg.Add(LeaveIf);
                        node.Result = CodeSeg.Count;
                        GetQuadruples(now.Right, Level);
                        //goto t
                        LeaveIf.Result = CodeSeg.Count;
                        FreeAllTempData();
                    }
                    FreeAllTempData();
                    break;
                case AstType.RepeatUntil:
                    node = new QuadrupleNode(GetOppositeJumpInstruction(now.Left.Info))
                    {
                        Result = CodeSeg.Count//当前位置
                    };
                    GetQuadruples(now.Right, Level);
                    node.Arg1 = GetArg(now.Left.Left, Level);
                    if (node.Arg1 == null)
                    {
                        GetQuadruples(now.Left.Left, Level);
                        node.Arg1 = ResultIndex;
                    }

                    if (node.Type != QuadrupleType.JNO && node.Type != QuadrupleType.JO)
                    {
                        node.Arg2 = GetArg(now.Left.Right, Level);
                        if (node.Arg2 == null)
                        {
                            GetQuadruples(now.Left.Right, Level);
                            node.Arg2 = ResultIndex;
                        }
                    }
                    CodeSeg.Add(node);
                    FreeAllTempData();
                    break;
                case AstType.WhileDo:
                    object op = now.Left.Info;
                    node = new QuadrupleNode(GetOppositeJumpInstruction(op))
                    {
                        Arg1 = GetArg(now.Left.Left, Level)
                    };
                    if (node.Arg1 == null)
                    {
                        GetQuadruples(now.Left.Left, Level);
                        node.Arg1 = ResultIndex;
                    }
                    if (node.Type != QuadrupleType.JNO && node.Type != QuadrupleType.JO)
                    {
                        node.Arg2 = GetArg(now.Left.Right, Level);
                        if (node.Arg2 == null)
                        {
                            GetQuadruples(now.Left.Right, Level);
                            node.Arg2 = ResultIndex;
                        }
                    }
                    CodeSeg.Add(node);
                    int location = CodeSeg.Count;
                    FreeAllTempData();

                    GetQuadruples(now.Right, Level);
                    node.Result = CodeSeg.Count;//while 条件不成立跳转
                    QuadrupleNode back = new QuadrupleNode(GetJumpInstruction(op))
                    {
                        Result = location
                    };
                    back.Arg1 = GetArg(now.Left.Left, Level);
                    if (back.Arg1 == null)
                    {
                        GetQuadruples(now.Left.Left, Level);
                        back.Arg1 = ResultIndex;
                    }
                    if (back.Type != QuadrupleType.JNO && back.Type != QuadrupleType.JO)
                    {
                        back.Arg2 = GetArg(now.Left.Right, Level);
                        if (back.Arg2 == null)
                        {
                            GetQuadruples(now.Left.Right, Level);
                            back.Arg2 = ResultIndex;
                        }
                    }

                    CodeSeg.Add(back);
                    node.Result = CodeSeg.Count;
                    FreeAllTempData();
                    break;
                case AstType.Expr:
                    var node1 = new QuadrupleNode(GetOperator(now.Info))
                    {
                        Arg2 = GetArg(now.Right, Level)
                    };
                    if (node1.Arg2 == null)
                    {
                        GetQuadruples(now.Right, Level);
                        node1.Arg2 = ResultIndex;
                    }
                    if (now.Left.Type == AstType.Minus)// -x -> 0 - x
                    {
                        node = new QuadrupleNode(QuadrupleType.Sub)
                        {
                            Arg1 = "#0",
                            Arg2 = GetArg(now.Left.Right, Level)
                        };
                        if (node.Arg2 == null)
                        {
                            GetQuadruples(now.Left.Right, Level);
                            node.Arg2 = ResultIndex;
                        }
                        node.Result = GetTemp();
                        CodeSeg.Add(node);
                        node1.Arg1 = node.Result;
                    }
                    else
                    {
                        node1.Arg1 = GetArg(now.Left, Level);
                        if (node1.Arg1 == null)
                        {
                            GetQuadruples(now.Left, Level);
                            node1.Arg1 = ResultIndex;
                        }
                    }
                    CodeSeg.Add(node1);
                    ResultIndex = GetTemp();
                    node1.Result = ResultIndex;
                    break;
                case AstType.Term:
                    node = new QuadrupleNode(GetOperator(now.Info))
                    {
                        Arg2 = GetArg(now.Right, Level)
                    };
                    if (node.Arg2 == null)
                    {
                        GetQuadruples(now.Right, Level);
                        node.Arg2 = ResultIndex;
                    }
                    node.Arg1 = GetArg(now.Left, Level);
                    if (node.Arg1 == null)
                    {
                        GetQuadruples(now.Left, Level);
                        node.Arg1 = ResultIndex;
                    }
                    node.Result = GetTemp();
                    ResultIndex = (int)node.Result;
                    CodeSeg.Add(node);
                    break;
                case AstType.Read: //需要自己平衡栈,Write & Read
                    ArrayList param = new ArrayList();
                    foreach (var i in (List<AstNode>)now.Info)
                    {
                        var n = new QuadrupleNode(VarSeg[i.Offset], Level);
                        param.Add(n);
                    }
                    CodeSeg.Add(new QuadrupleNode(QuadrupleType.Read, param));
                    break;
                case AstType.Write:
                    param = new ArrayList();
                    foreach (var i in (List<AstNode>)now.Info)
                    {
                        if (i.Type == AstType.NUM)
                        {
                            param.Add(i.Info);
                        }
                        else if (i.Type == AstType.Var)
                        {
                            var n = new QuadrupleNode(VarSeg[i.Offset], Level);
                            param.Add(n);
                        }
                        else if (i.Type == AstType.Const)
                        {
                            param.Add("#" + ConstSeg[i.Offset]);
                        }
                    }
                    CodeSeg.Add(new QuadrupleNode(QuadrupleType.Write, param));
                    break;
                default:
                    throw new Exception($"{now.Type}");
            }

        }

        private void LocateJumpNodeAndDetermineNodeOffset()
        {
            for (int i = 0; i < CodeSeg.Count; ++i)
            {
                QuadrupleNode node = CodeSeg[i] as QuadrupleNode;
                node.Offset = i;
                /*
                if (IsJumpInstruction(node.Type))
                {
                    if (node.Result is int)
                    {
                        int tar = (int)node.Result;
                        if (tar >= CodeSeg.Count)
                        {
                            node.Result = null;
                        }
                        else
                        {
                            node.Result = CodeSeg[tar];
                        }
                    }
                }
                */
            }
        }

        private QuadrupleType GetOppositeJumpInstruction(object info)
        {
            if (info is string)
            {
                switch ((string)info)
                {
                    case "<=":
                        return QuadrupleType.JG;
                    case ">=":
                        return QuadrupleType.JL;
                    case "<>":
                        return QuadrupleType.JE;
                    case "odd":
                        return QuadrupleType.JNO;
                }
            }
            else if (info is char)
            {
                switch ((char)info)
                {
                    case '<':
                        return QuadrupleType.JGE;
                    case '>':
                        return QuadrupleType.JLE;
                    case '=':
                        return QuadrupleType.JNE;
                }
            }
            throw new Exception($"Unexpected {info},Exprect Relation Operator");
        }

        private QuadrupleType GetJumpInstruction(object info)
        {
            if (info is string)
            {
                switch ((string)info)
                {
                    case "<=":
                        return QuadrupleType.JLE;
                    case ">=":
                        return QuadrupleType.JGE;
                    case "<>":
                        return QuadrupleType.JNE;
                    case "odd":
                        return QuadrupleType.JO;
                }
            }
            else if (info is char)
            {
                switch ((char)info)
                {
                    case '<':
                        return QuadrupleType.JL;
                    case '>':
                        return QuadrupleType.JG;
                    case '=':
                        return QuadrupleType.JE;
                }
            }
            throw new Exception($"Unexpected {info},Exprect Relation Operator");
        }

        private QuadrupleType GetOperator(object info)
        {
            if (info is char)
            {
                switch ((char)info)
                {
                    case '+':
                        return QuadrupleType.Add;
                    case '-':
                        return QuadrupleType.Sub;
                    case '*':
                        return QuadrupleType.Mul;
                    case '/':
                        return QuadrupleType.Div;
                }
            }
            throw new Exception($"Unexpected {info} in GetOperator");
        }

        private bool IsJumpInstruction(QuadrupleType type)
        {
            return Convert.ToInt32(type) <= Convert.ToInt32(QuadrupleType.JMP);
        }

        private Parser parser;

        private AstNode Root;

        private List<QuadrupleNode> VarSeg, ProcedureSeg;

        private List<int> ConstSeg;

        private List<QuadrupleNode> CodeSeg;

        private int FreeDataIndex;

        private int ResultIndex;//记录上一次计算后结果存放位置,用数字表示临时变量

        public static readonly int MagicNumber = 2085433141;

        internal static readonly int MaxTempDataNum = 2333;

        private int CodeEntrance;

        public ErrorMsgList ErrorMsg;
        #endregion
    }
}