using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class PCodeGeneraotr
    {
        public PCodeGeneraotr()
        {
            GetIL = new ILGenerator();
            Programs = new List<PNode>();
        }

        public List<PNode> GenerateCode(string Text, int Level)
        {
            GetIL.GenerateCode(Text, Level);
            NumOfError = GetIL.NumOfError;
            if (GetIL.NumOfError > 0)
            {
                return null;
            }
            ErrorMsg = GetIL.ErrorMsg;
            GetIL.GetInfo(ref CodeSeg, ref VarSeg);
            GetPCode();
            return Programs;
        }

        public string GetErrorMsgString()
        {
            return GetIL.GetErrorMsgString();
        }

        public string GetPCodeString()
        {
            if (Programs.Count == 0)
            {
                return "Please correct all errors or Call 'GeneratePCode' first";
            }
            int index = 0;
            StringBuilder sb = new StringBuilder();
            foreach (var i in Programs)
            {
                sb.Append(string.Format("{0,-3}-> ", index++));
                sb.Append(string.Format("{0,-6} ", Enum.GetName(i.INS.GetType(), i.INS)));
                switch (i.INS)
                {
                    case PCode.EXP:
                    case PCode.HALT:
                    case PCode.WRT:
                        sb.Append("\n");
                        break;
                    default:
                        switch (i.DataType)
                        {
                            case 1:
                            case 4:
                                sb.Append("0, ");
                                sb.Append(i.Arg);
                                sb.Append('\n');
                                break;
                            case 2:
                                sb.Append($"t{i.Arg}\n");
                                break;
                            case 3:
                                //if (i.INS == PCode.LOD || i.INS == PCode.STO || i.INS == PCode.RED)
                                sb.Append($"{i.Level}, {i.Offset}\n");
                                break;
                            case 5:
                                sb.Append($"0, {i.Arg}\n");
                                break;
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public int NumOfError { get; private set; }

        private void GetPCode()
        {
            Programs.Clear();
            foreach (var i in CodeSeg)
            {
                Translate(i);
            }
            foreach (var i in Programs)
            {
                if (i.INS == PCode.JMP || i.INS == PCode.JPC)
                {
                    try
                    {
                        i.Arg = CodeSeg[i.Arg].Start;
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            Programs[Programs.Count - 1] = new PNode(PCode.HALT);
        }

        private void Translate(QuadrupleNode Node)
        {
            Node.Start = Programs.Count;
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
                    if (Node.Arg2 is int)
                    {
                        var node = new PNode(PCode.LOD, (int)Node.Arg2, 2);
                        Add(node);
                    }
                    else if (Node.Arg2 is string)
                    {
                        int arg = int.Parse(((string)Node.Arg2).Substring(1));
                        Add(new PNode(PCode.LIT, arg, 1));
                    }
                    else
                    {
                        Add(new PNode(PCode.LOD, ((QuadrupleNode)(Node.Arg2)).Offset, 3) { Offset = ((QuadrupleNode)(Node.Arg2)).AddressOffset, Level = ((QuadrupleNode)(Node.Arg2)).Level });
                    }
                    var qnode = (QuadrupleNode)(Node.Arg1);
                    Add(new PNode(PCode.STO, qnode.Offset, 3) { Offset = qnode.AddressOffset, Level = qnode.Level });
                    break;
                case QuadrupleType.Write:
                    var list = Node.Arg1 as ArrayList;
                    foreach (var i in list)
                    {
                        if (i is string)
                        {
                            Add(new PNode(PCode.LIT, Convert.ToInt32(((string)i).Substring(1)), 1));
                            Add(new PNode(PCode.WRT));
                            continue;
                        }
                        var param = i as QuadrupleNode;
                        Add(new PNode(PCode.LOD, param.Offset, 3) { Offset = param.AddressOffset, Level = param.Level });
                        Add(new PNode(PCode.WRT));
                    }
                    break;
                case QuadrupleType.Read:
                    list = Node.Arg1 as ArrayList;
                    foreach (var i in list)
                    {
                        var param = i as QuadrupleNode;
                        Add(new PNode(PCode.RED, param.Offset, 3) { Offset = param.AddressOffset, Level = param.Level });
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
            LoadArg(Node);
            switch (Node.Type)//根据条件选择相反的指令，因为是if False跳转
            {
                case QuadrupleType.JNO:
                    Add(new PNode(PCode.OPR, 6, 5));
                    break;
                case QuadrupleType.JO:
                    Add(new PNode(PCode.OPR, 7, 5));
                    break;
                case QuadrupleType.JNE:
                    Add(new PNode(PCode.OPR, 8, 5));
                    break;
                case QuadrupleType.JE:
                    Add(new PNode(PCode.OPR, 9, 5));
                    break;
                case QuadrupleType.JGE:
                    Add(new PNode(PCode.OPR, 10, 5));
                    break;
                case QuadrupleType.JL:
                    Add(new PNode(PCode.OPR, 11, 5));
                    break;
                case QuadrupleType.JLE:
                    Add(new PNode(PCode.OPR, 12, 5));
                    break;
                case QuadrupleType.JG:
                    Add(new PNode(PCode.OPR, 13, 5));
                    break;
            }
            Add(new PNode(PCode.JPC, (int)Node.Result, 4));
        }

        private void TranslateOpr(QuadrupleNode Node)
        {
            LoadArg(Node);
            switch (Node.Type)
            {
                case QuadrupleType.Add:
                    Add(new PNode(PCode.OPR, 2, 5));
                    break;
                case QuadrupleType.Sub:
                    Add(new PNode(PCode.OPR, 3, 5));
                    break;
                case QuadrupleType.Mul:
                    Add(new PNode(PCode.OPR, 4, 5));
                    break;
                case QuadrupleType.Div:
                    Add(new PNode(PCode.OPR, 5, 5));
                    break;
            }
            Add(new PNode(PCode.STO, (int)Node.Result, 2));
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
                Add(new PNode(PCode.LOD, t.Offset, 3) { Offset = t.AddressOffset, Level = t.Level });
            }
            if (Node.Arg2 == null) return;
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
                Add(new PNode(PCode.LOD, t.Offset, 3) { Offset = t.AddressOffset, Level = t.Level });
            }
        }

        private void Add(PNode node)
        {
            Programs.Add(node);
        }

        internal List<QuadrupleNode> VarSeg;
        private List<PNode> Programs;
        private ILGenerator GetIL;
        private List<QuadrupleNode> CodeSeg;
        private int IsJump = Convert.ToInt32(QuadrupleType.JMP);
        public ErrorMsgList ErrorMsg;
    }
}
