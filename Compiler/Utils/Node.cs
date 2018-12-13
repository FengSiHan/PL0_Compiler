using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class AstNode
    {
        public object Info;
        public AstType Type;
        public AstNode Left, Right;
        public Position Location;
        public bool Initialized;
        public int Offset;
        public AstNode()
        {
        }

        public AstNode(AstType type, AstNode left = null, AstNode right = null, Object info = null, Position location = null)
        {
            Type = type;
            Left = left;
            Right = right;
            Info = info;
            Location = location;
            Initialized = false;
        }

        public AstNode(AstType type, Position location, AstNode left = null, AstNode right = null, Object info = null)
        {
            Type = type;
            Left = left;
            Right = right;
            Info = info;
            Location = location;
            Initialized = false;
        }
        public override string ToString()
        {
            if (Type == AstType.IdDefine || Type == AstType.NUM)
            {
                return (string)Info;
            }
            return base.ToString();

        }
    }

    public class QuadrupleNode
    {
        public QuadrupleType Type;
        public Object Arg1, Arg2;//Arg为Node表示节点为一个运算结果，为string表示数，以#开头
        public QuadrupleNode CurrentValue;
        public object Value;
        public int Offset;
        public int? Result;//表示临时变量，或者跳转地址
        public bool Active;
        public int Level;
        public int AddressOffset;
        internal Optimize.Block JumpAddr;
        internal int Start;
        public QuadrupleNode(QuadrupleType type, Object arg1 = null, Object arg2 = null)
        {
            Type = type;
            Arg1 = arg1;
            Arg2 = arg2;
            Active = false;
            CurrentValue = null;
            Result = null;//表示未初始化
        }
        public QuadrupleNode(QuadrupleNode old, int level)
        {
            Type = old.Type;
            Arg1 = old.Arg1;
            Arg2 = old.Arg2;
            Value = old.Value;
            Offset = old.Offset;
            Level = level - old.Level;
            AddressOffset = old.AddressOffset;
        }
    }

    public class DAGNode : IEqualityComparer<DAGNode>, IEquatable<DAGNode>
    {
        public DAGType Type;
        public int Offset;//用于识别变量
        public DAGNode Left, Right, CurrentValue;//附加到CurrentValue节点的Tags内
        public List<DAGNode> Tags;//只有表达式节点具有该实例
        public long Value;
        public int SN;//独一无二递增的序列号
        public ArrayList list;//为Read&Write函数准备
        public bool Active;
        internal Optimize.Block Host; //所属基本块
        private int? hash;
        internal DAGNode(DAGType type, int SerialNumber, Optimize.Block host)
        {
            Tags = new List<DAGNode>();
            Type = type;
            SN = SerialNumber;
            if (Type == DAGType.Num)
            {
                CurrentValue = this;
            }
            if (Type == DAGType.Temp)
            {
                Active = false;
            }
            else
            {
                Active = true;
            }
            hash = null;
            Host = host;
        }
        public void Add(DAGNode node)
        {
            Tags.Add(node);
            node.CurrentValue = this;
        }
        public DAGNode Find(DAGNode node)
        {
            foreach (var i in Tags)
            {
                if (node == i)
                {
                    return i;
                }
            }
            return null;
        }
        public DAGNode Find(int SerialNumber)
        {
            foreach (var i in Tags)
            {
                if (SerialNumber == i.SN)
                {
                    return i;
                }
            }
            return null;
        }
        public void Remove(DAGNode node)
        {
            Tags.Remove(node);
        }
        public void Remove(int SerialNumber)
        {
            foreach (var i in Tags)
            {
                if (SerialNumber == i.SN)
                {
                    Tags.Remove(i);
                    return;
                }
            }
        }
        public static bool operator ==(DAGNode a, DAGNode b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
            {
                return true;
            }
            else if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
            {
                return false;
            }
            return a.Equals(b);
        }

        public static bool operator !=(DAGNode a, DAGNode b)
        {
            return !(a == b);
        }
        public override bool Equals(object obj)
        {
            DAGNode node = obj as DAGNode;
            if (ReferenceEquals(node, null))
            {
                return false;
            }
            return CmpRecursive(this, node);
        }
        private bool CmpRecursive(DAGNode a, DAGNode b)
        {
            if (a.Type != b.Type)
            {
                return false;
            }
            if (a.Type == DAGType.Var)//指向同一个引用
            {
                return a.Offset == b.Offset;
            }
            else if (a.Type == DAGType.Anonymous)
            {
                return a.Offset == b.Offset;
            }
            else if (a.Type == DAGType.Num)
            {
                return a.Value == b.Value;
            }
            else if (a.Type == DAGType.Temp)
            {
                return a.Value == b.Value;
            }
            else if (a.Type == DAGType.Read || a.Type == DAGType.Write)
            {
                ArrayList al = a.list, bl = b.list;
                if (al.Count != bl.Count)
                {
                    return false;
                }
                for (int i = 0; i < al.Count; ++i)
                {
                    if (al[i].GetType() != bl[i].GetType())
                    {
                        return false;
                    }
                    else
                    {
                        if (al[i] is QuadrupleNode)
                        {
                            if (((QuadrupleNode)al[i]).Offset != ((QuadrupleNode)bl[i]).Offset)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if ((int)al[i] != (int)bl[i])
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            else if ((CmpRecursive(a.Left, b.Left) && CmpRecursive(a.Right, b.Right)) == false)
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            if (hash != null)
            {
                return (int)hash;
            }
            var hashCode = ILGenerator.MagicNumber;
            hashCode ^= Type.GetHashCode() << 24;
            if (Type == DAGType.Var || Type == DAGType.Anonymous)
            {
                hashCode ^= Offset.GetHashCode();
            }
            else if (Type == DAGType.Num)
            {
                hashCode ^= Value.GetHashCode();
            }
            if (Left != null)
            {
                hashCode ^= Left.GetHashCode();
            }
            if (Right != null)
            {
                hashCode ^= Right.GetHashCode();
            }
            hash = hashCode;
            return hashCode;
        }

        public bool Equals(DAGNode x, DAGNode y)
        {
            if (ReferenceEquals(x, null) && ReferenceEquals(y, null))
            {
                return true;
            }
            else if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
            {
                return false;
            }
            return CmpRecursive(x, y);
        }

        public int GetHashCode(DAGNode obj)
        {
            return obj.GetHashCode();
        }

        public bool Equals(DAGNode other)
        {
            return CmpRecursive(this, other);
        }
    }

    public class PNode
    {
        public readonly PCode INS;

        public int Arg;

        public readonly int DataType;

        public int Offset;

        public int Level;

        /// <summary>
        /// Type: 1:立即数，2:临时变量，3:变量，4:地址, 5:类型
        /// </summary>
        /// <param name="ins"></param>
        /// <param name="arg"></param>
        /// <param name="type"> </param>
        public PNode(PCode ins, int arg, int type)
        {
            DataType = type;
            Arg = arg;
            INS = ins;
        }

        public PNode(PCode ins)
        {
            INS = ins;
        }
    }

    public enum PCode
    {
        //lit 0, a    : load constant a    读取常量a到数据栈栈顶
        //opr 0, a     : execute operation a    执行a运算
        //lod l, a : load variable l, a    读取变量放到数据栈栈顶，变量的相对地址为a，层次差为1
        //sto l, a : store variable l, a    将数据栈栈顶内容存入变量，变量的相对地址为a，层次差为1
        //cal 0, a    : call procedure a at level l    调用过程，过程入口指令为a, 层次差为1
        //jmp a    : jump to a    无条件跳转到指令地址a
        //jpc a    : jump conditional to a    条件转移到指令地址a
        //red l, a    : read variable l, a    读数据并存入变量，
        //wrt      : write stack-top    将栈顶内容输出
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
        MOD,
        NOT, //^
        OPR
    }

    public enum DAGType
    {
        Sub,
        Mul,
        Add,
        Div,
        Num,
        Var,
        Temp,//t0 t1  t2...临时变量
        Assign,
        Write,
        Read,
        Call,
        Anonymous,
        AutoTemp
    }

    public enum QuadrupleType
    {
        JG,
        JGE,
        JL,
        JLE,
        JE,
        JNE,
        JO,
        JNO,//非奇
        JMP,
        Return,
        Call,
        Mul,
        Div,
        Add,
        Sub,
        Write,
        Read,
        Var,
        Num,
        Proc,
        Assign,
        Opr
    }

    public enum AstType
    {
        Define,
        IdDefine,
        ConstDefine,
        VarDefine,
        Const,
        Var,
        ProcDefine,// Procedure
        ProcsDefine,// Procedures
        Statement,
        Statements,
        Expr,
        Assign,
        Term,
        Condition,
        IfElse, // left is a node contains condition and action, right is 'else' action(may be null)
        WhileDo,
        Call,
        RepeatUntil,
        Read,
        Write,
        NUM,
        VarID,   //object is string  , and is its name
        ConstID,
        ProcID,
        UnknownID,
        SubProgram,
        Minus
    }

    public enum TokenType
    {
        ID,
        NUM,
        STRING,
        OP,
        BRACKET,
        SEMICOLON,
        ASSIGN,
        PERIOD,
        COMMA
    }
}