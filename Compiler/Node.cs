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
        public ExprType Type;
        public AstNode Left, Right;
        public Position Location;
        public bool Initialized;
        public int Offset;
        public AstNode()
        {
        }

        public AstNode(ExprType type, AstNode left = null, AstNode right = null, Object info = null, Position location = null)
        {
            Type = type;
            Left = left;
            Right = right;
            Info = info;
            Location = location;
            Initialized = false;
        }

        public AstNode(ExprType type, Position location, AstNode left = null, AstNode right = null, Object info = null)
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
            if (Type == ExprType.IdDefine || Type == ExprType.NUM)
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
        internal Optimize.Block JumpAddr;
        public QuadrupleNode(QuadrupleType type, Object arg1 = null, Object arg2 = null)
        {
            Type = type;
            Arg1 = arg1;
            Arg2 = arg2;
            Active = false;
            CurrentValue = null;
            Result = null;//表示未初始化
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
        Assign
    }
}