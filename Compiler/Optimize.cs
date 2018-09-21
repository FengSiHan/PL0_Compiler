using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compiler
{
    public class Optimize
    {
        internal class Block
        {
            internal int Start, End;//[start,end]
            internal List<DAGNode> DAG;
            internal List<Block> Prev; //从何处跳转而来
            internal List<Block> Next; //该基本块结束后跳转至何处,这是条件跳转条件不成立时跳转的地址
            internal List<DAGNode> OutAvailableExpr;
            internal List<DAGNode> InAvailableExpr;
            internal List<DAGNode> ActiveVar;//保存偏移量
            internal List<DAGNode> AvailableExpr;
            internal List<DAGNode> ExprSet;//运算节点左右可能为temp,再获取其表达式节点
            internal List<DAGNode> InputVar;
            internal int Index;//在流程图中寻找强连通分量时DFS访问的顺序
            internal int Addr;//表示在Blocks中的偏移量
            internal bool AutoGenerate;//指示是否为循环优化自动生成
            internal List<QuadrupleNode> JumpInsAddr;
            internal Block()
            {
                Prev = new List<Block>();
                Next = new List<Block>();
                OutAvailableExpr = new List<DAGNode>();
                ActiveVar = new List<DAGNode>();
                AvailableExpr = new List<DAGNode>();
                ExprSet = new List<DAGNode>();
                InputVar = new List<DAGNode>();
                Index = -1;
                AutoGenerate = false;
                JumpInsAddr = new List<QuadrupleNode>();
            }
        }
        private class NaturalLoop
        {
            internal List<NaturalLoop> SubLoop; //子循环
            internal Block PrevHeader;  //外提代码存放处
            internal List<int> InnerBlock;      //循环内包含基本块
            internal int LoopEntrance;
            internal Dictionary<int, InductionVar> BaseInductionVar;
            internal List<Triple> InductionTriple;
            internal bool Contained;
            internal Dictionary<int, Invariant> AssignDict;
            internal BackEdge Edge;
            internal NaturalLoop(BackEdge edge)
            {
                SubLoop = new List<NaturalLoop>();
                PrevHeader = new Block();
                PrevHeader.DAG = new List<DAGNode>();
                InnerBlock = new List<int>();
                BaseInductionVar = new Dictionary<int, InductionVar>();
                InductionTriple = new List<Triple>();
                Edge = edge;
                LoopEntrance = -1;
                Contained = false;
                PrevHeader.AutoGenerate = true;
            }
            /// <summary>
            /// 比较两个自然循环的关系
            /// -2 : 循环入口相同，this 真包含于 loop，this作为loop的子循环
            /// -1 : 循环入口相同，this 真包含 loop，loop作为this的子循环
            ///  0 : 循环入口相同，this 等于 loop 或者 不完全相等，都作合并处理
            ///  1 : 循环入口不同，this 真包含 loop, loop作为this的子循环
            ///  2 : 循环入口不同，this 真包含于 loop ,this作为loop的子循环
            ///  3 : 循环入口不同，this 独立于 loop，this 和 loop是独立循环
            ///  4 : 暂定为Error
            /// </summary>
            /// <param name="loop"></param>
            /// <returns></returns>
            internal int CompareTo(NaturalLoop loop)
            {
                if (LoopEntrance == loop.LoopEntrance)
                {
                    int res = InnerBlock.Contain(loop.InnerBlock);
                    switch (res)
                    {
                        case 1:
                            return -1;
                        case 2:
                            return -2;
                        case 3:
                            return 0;
                        case 4:
                            return 0;
                        default:
                            return 4;
                    }
                }
                else
                {
                    int res = InnerBlock.Contain(loop.InnerBlock);
                    switch (res)
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                        case 3:
                            return 4; //入口不同最终相同怕是有鬼
                        case 4:
                            return 3;
                        default:
                            return 4;
                    }
                }
            }
        }
        private struct BackEdge
        {
            internal Block Start, End;
            internal BackEdge(Block start, Block end)
            {
                Start = start;
                End = end;
            }
        }
        private class Invariant
        {
            internal int Offset;
            internal List<DAGNode> Host;
            internal int Counter;
            internal DAGNode Node;
            internal int Addr;
            internal Invariant(List<DAGNode> host, int addr, DAGNode node, int offset)
            {
                Offset = offset;
                Host = host;
                Counter = 1;
                Addr = addr;
                Node = node;
            }
        }
        internal class Triple
        {
            internal struct Pair
            {
                internal int Offset, Coefficient;
                internal Pair(int o, int c)
                {
                    Offset = o;
                    Coefficient = c;
                }
            }


            internal DAGNode OriginNode;
            internal List<DAGNode> Host;
            internal int Offset;//变量的偏移量
            internal int Increase;//增量
            internal int Init;//初始值
            internal int Addr;
            internal List<Pair> BaseInductionList;//含有的归纳表达式列表,以及系数
            internal Triple(List<DAGNode> host, int addr, DAGNode originNode, int offset, int incr)
            {
                OriginNode = originNode;
                Host = host;
                Offset = offset;
                Increase = incr;
                BaseInductionList = new List<Pair>();
                Init = 0;
                Addr = addr;
            }
            internal void ChangeStep(int incr)
            {
                Increase += incr;
            }
            internal void AddBaseInduction(int offset, int c)
            {
                BaseInductionList.Add(new Pair(offset, c));
            }
            internal void ChangeInit(int value)
            {
                Init += value;
            }
        }

        private class InductionVar
        {
            internal List<DAGNode> Host;
            internal int Offset;//变量在变量表中的偏移量
            internal DAGNode Incr;
            internal DAGNode Node;//所在赋值节点
            internal DAGType Operator;
            internal int Addr;//在Host中的地址(偏移量)
            internal InductionVar(List<DAGNode> host, int addr, DAGNode node, int offset, DAGNode incr, DAGType op)
            {
                Host = host;
                Offset = offset;
                Incr = incr;
                Node = node;
                Operator = op;
                Addr = addr;
            }
        }


        internal Block EntranceBlock;
        public Optimize(List<QuadrupleNode> codeseg, List<QuadrupleNode> varseg, int code_entrance)
        {
            CodeSeg = codeseg;
            VarSeg = varseg;
            CodeEntrance = code_entrance;
            Blocks = new List<Block>();
            visited = new bool[CodeSeg.Count];
            for (int i = 0; i < CodeSeg.Count; ++i)
            {
                visited[i] = false;
            }
            if (CodeEntrance != -2)
            {
                DivideBlock();
                foreach (var i in Blocks)
                {
                    if (i.Start == CodeEntrance)
                    {
                        EntranceBlock = i;
                        break;
                    }
                }
            }
        }
        /*
         * 优化只改变block中的DAG
         * 压缩代码空间会导致基本块重组
         * 之前的引用该修改的修改
         */
        public void LocalOptimization()
        {
            RemoveDeadCode();
            foreach (var block in Blocks)
            {
                /*
                 * 对于QNode:Left,Right,要么为#xxx,表示立即数，要么为x，表示一个临时变量,一个基本块内保证临时变量名不重复(通过控制调用FreeAllTempData()时机)
                 * Mov a1,a2  => a2 = a1  a1必然是数或者是变量,
                 * 赋值 a1,a2  => a1 = a2
                 */
                if (block.Start > block.End) //删除死代码可能导致block参数End小于start
                {
                    continue;
                }
                //ResetSN();
                List<DAGNode> Nodes = new List<DAGNode>();//储存DAG图的所有节点，SN表示生成顺序，用于生成代码时排序
                block.DAG = Nodes;
                Action<DAGNode> CheckConstReference = (k) =>
                {
                    if (k.Type != DAGType.Num)
                    {
                        return;
                    }
                    if (k.Tags.Count <= 1)//刚刚创建或者被引用次数为0,1
                    {
                        Nodes.Remove(k);
                    }
                    else
                    {
                        k.Tags.RemoveAt(k.Tags.Count - 1);//移除最后一个，即刚刚添加的
                    }
                };
                for (int i = block.Start; i < block.End || i <= block.End && NotJumpOrReturn(i); ++i) //DAG中不构建最后一句Jump或者Return，最后一句可能是普通语句
                {
                    QuadrupleNode CodeNode = CodeSeg[i];
                    DAGNode left, right;
                    left = GetNode(CodeNode.Arg1, Nodes, block);
                    right = GetNode(CodeNode.Arg2, Nodes, block);
                    if (CodeNode.Type != QuadrupleType.Write
                        && CodeNode.Type != QuadrupleType.Read
                        && CodeNode.Type != QuadrupleType.Call
                        && (left == null || right == null))
                    {
                        throw new Exception("Error happened during optimization");
                    }
                    if (CodeNode.Type != QuadrupleType.Read
                        && CodeNode.Type != QuadrupleType.Write
                        && CodeNode.Type != QuadrupleType.Call
                        && GetValue(left).Type == DAGType.Num
                        && GetValue(right).Type == DAGType.Num)//可能是间接的常量传播
                    {
                        long result = 0;
                        left = GetValue(left);
                        right = GetValue(right);
                        switch (CodeNode.Type)
                        {
                            case QuadrupleType.Sub:
                                result = left.Value - right.Value;
                                break;
                            case QuadrupleType.Add:
                                result = left.Value + right.Value;
                                break;
                            case QuadrupleType.Mul:
                                result = left.Value * right.Value;
                                break;
                            case QuadrupleType.Div:
                                result = left.Value / right.Value;
                                break;
                        }
                        CheckConstReference(left);
                        CheckConstReference(right);
                        DAGNode Target = GetNode(CodeNode.Result, Nodes, block);
                        DAGNode Result = GetNode($"#{result}", Nodes, block);
                        Target.CurrentValue = Result;
                        Result.Add(Target);
                    }
                    else
                    {
                        if (CodeNode.Type == QuadrupleType.Assign)
                        {
                            DAGNode node = new DAGNode(DAGType.Assign, GetSN(), block)
                            {
                                Left = left,
                                Right = right
                            };
                            Nodes.Add(node);
                            if (left.CurrentValue != null)
                            {
                                left.CurrentValue.Remove(left);
                            }
                            if (right.Type == DAGType.Num)
                            {
                                left.CurrentValue = right;
                            }
                            else if (right.Type == DAGType.Var)
                            {
                                left.CurrentValue = GetValue(right);
                            }
                            else if (right.Type == DAGType.Temp)
                            {
                                left.CurrentValue = GetValue(right);
                            }
                            else
                            {
                                throw new Exception("First argument of Assign should be Num or Var!");
                            }
                            /*
                            if (left.Type == DAGType.Var)
                            {
                                // d = d + d 
                                // Mov d,t0
                                // Mov d,t1
                                // Add t0,t1,t2
                                // Assign d,t2
                                //至此t0,t1失效，以后对D的引用转为t1+t2,但已经失效 
                                foreach (var k in left.Tags)
                                {
                                    k.CurrentValue = null;
                                }
                                left.Tags.Clear();
                            }
                            */
                            left.CurrentValue.Add(left);
                        }
                        else if (CodeNode.Type == QuadrupleType.Write)
                        {
                            DAGNode node = new DAGNode(DAGType.Write, GetSN(), block)
                            {
                                list = CodeNode.Arg1 as ArrayList
                            };
                            Nodes.Add(node);
                        }
                        else if (CodeNode.Type == QuadrupleType.Read)
                        {
                            DAGNode node = new DAGNode(DAGType.Read, GetSN(), block)
                            {
                                list = CodeNode.Arg1 as ArrayList
                            };
                            Nodes.Add(node);
                        }
                        else if (CodeNode.Type == QuadrupleType.Call)
                        {
                            DAGNode node = new DAGNode(DAGType.Call, GetSN(), block)
                            {
                                Value = (long)CodeNode.Result
                            };
                            Nodes.Add(node);
                        }
                        else // 删除公共子表达式
                        {
                            DAGNode node = null;
                            switch (CodeNode.Type)
                            {
                                case QuadrupleType.Add:
                                    node = new DAGNode(DAGType.Add, GetSN(), block);
                                    break;
                                case QuadrupleType.Sub:
                                    node = new DAGNode(DAGType.Sub, GetSN(), block);
                                    break;
                                case QuadrupleType.Div:
                                    node = new DAGNode(DAGType.Div, GetSN(), block);
                                    break;
                                case QuadrupleType.Mul:
                                    node = new DAGNode(DAGType.Mul, GetSN(), block);
                                    break;
                            }
                            DAGNode Left = GetValue(left), Right = GetValue(right);
                            //Left和Right 为运算节点，输入变量，数
                            DAGNode Result = GetNode(CodeNode.Result, Nodes, block);
                            Result.CurrentValue?.Remove(Result);
                            //运算强度削减,把2~3个的乘法变为加法
                            //一定程度的代数恒等式变换
                            //直接将Result节点的值赋值为可以计算出来的值

                            /* 0 未赋值/未优化，照常生成
                             * 1 左右节点已经赋值
                             *-1 结果节点已经赋值
                             */
                            int status = 0;
                            if (node.Type == DAGType.Mul)
                            {
                                Action<DAGNode, DAGNode> MulOptimization = (num, value) =>
                                {
                                    switch (num.Value)
                                    {
                                        //这里要删除不再被引用的常数节点
                                        case 0:
                                            CheckConstReference(num);
                                            Result.CurrentValue = GetNode("#0", Nodes, block);
                                            Result.CurrentValue.Add(Result);
                                            status = -1;
                                            break;
                                        case 1:
                                            CheckConstReference(num);
                                            Result.CurrentValue = value;
                                            Right.CurrentValue.Add(Result);
                                            status = -1;
                                            break;
                                        case 2:
                                            CheckConstReference(num);
                                            node.Left = value;
                                            node.Right = value;
                                            node.Type = DAGType.Add;
                                            status = 1;
                                            break;
                                        default:
                                            //由于不支持移位操作，暂时搁置
                                            break;
                                    }
                                };
                                if (Left.Type == DAGType.Num)
                                {
                                    MulOptimization(Left, Right);
                                }
                                else if (right.Type == DAGType.Num)
                                {
                                    MulOptimization(Right, Left);
                                }
                            }
                            else if (node.Type == DAGType.Div)
                            {
                                if (Left.Type == DAGType.Num && Left.Value == 0)
                                {
                                    CheckConstReference(Left);
                                    Result.CurrentValue = GetNode("#0", Nodes, block);
                                    Result.CurrentValue.Add(Result);
                                    status = -1;
                                }
                                else if (Right.Type == DAGType.Num && Right.Value == 1)
                                {
                                    CheckConstReference(Right);
                                    Result.CurrentValue = Left;
                                    Result.CurrentValue.Add(Result);
                                    status = -1;
                                }
                                else if (GetValue(Left) == GetValue(Right))
                                {
                                    Result.CurrentValue = GetNode("#1", Nodes, block);
                                    Result.CurrentValue.Add(Result);
                                    status = -1;
                                }
                            }
                            else if (node.Type == DAGType.Sub)
                            {
                                if (Left == Right)
                                {
                                    Result.CurrentValue = GetNode("#0", Nodes, block);
                                    Result.CurrentValue.Add(Result);
                                    status = -1;
                                }
                            }
                            switch (status)
                            {
                                case 0:
                                    node.Left = Left;
                                    node.Right = Right;
                                    break;
                                case 1:
                                    break;
                                case -1:
                                    continue;
                            }
                            bool find = false;

                            foreach (var k in block.ExprSet)
                            {
                                if (k.Equals(node))
                                {
                                    Result.CurrentValue = k;
                                    find = true;
                                    break;
                                }
                            }
                            if (!find && false) //交换左右节点顺序再查找一次
                            {

                                var tmp = node.Right;
                                node.Right = node.Left;
                                node.Left = tmp;
                                foreach (var k in block.ExprSet)
                                {
                                    if (k == node)
                                    {
                                        Result.CurrentValue = k;
                                        find = true;
                                        break;
                                    }
                                }
                                //这里还原不还原似乎问题不大，考虑写表达式的时候可能习惯性和前面一样，所以还是交换回来
                                if (!find)
                                {
                                    tmp = node.Right;
                                    node.Right = node.Left;
                                    node.Left = tmp;
                                }
                            }
                            if (!find)
                            {
                                Result.CurrentValue = node;
                                block.ExprSet.Add(GetValue(Result));
                            }

                            Result.CurrentValue.Add(Result);

                        }
                    }
                }
                DeleteUselessTemp(block);
            }
        }
        public void GlobalOptimization()
        {
            //时间复杂度O(n^4)警告
            IterativeAliveVarAnalysis();
            //完成删除死代码后进行全局公共子表达式替换
            //删除死代码
            foreach (var block in Blocks)
            {
                var DAG = block.DAG;
                var Active = block.ActiveVar;
                var deleteList = DAG.Except(Active);
                List<DAGNode> deleteNode = new List<DAGNode>();
                List<DAGNode> temp = new List<DAGNode>();
                //获取对不活跃变量的赋值
                foreach (var i in DAG)
                {
                    if (i.Type == DAGType.Assign)
                    {
                        if (deleteList.Contains(i.Left))
                        {
                            temp.Add(i);
                        }
                    }
                }
                foreach (var i in temp)//对不活跃变量的赋值可以去掉，并且递归删除其CurrentValue
                {
                    DAG.Remove(i);
                    if (i.Right?.Tags.Count <= 1)//只有当前节点引用了这个节点，可以删除
                    {
                        deleteNode.Add(i.Right);
                    }
                }
                Queue<DAGNode> WorkQueue = new Queue<DAGNode>();
                foreach (var i in deleteNode)
                {
                    WorkQueue.Enqueue(i);
                }
                while (WorkQueue.Count != 0)
                {
                    var node = WorkQueue.Dequeue();
                    if (IsExpressionNode(node))
                    {
                        block.ExprSet.Remove(node);
                        if (node.Left.Tags.Count <= 1) //只存在一个引用则删除
                        {
                            WorkQueue.Enqueue(node.Left);
                        }
                        if (node.Right.Tags.Count <= 1)
                        {
                            WorkQueue.Enqueue(node.Right);
                        }
                    }
                    else
                    {
                        DAG.Remove(node);
                    }
                }
            }
            IterativeAvailableExpressionAnalysis();

            //公共子表达式的值指向一个新创建的变量，匿名
            foreach (var block in Blocks)
            {
                foreach (var nowNode in block.ExprSet)
                {
                    DAGNode CommonExpr = null;
                    foreach (var prevNode in block.InAvailableExpr)
                    {
                        if (prevNode == nowNode)
                        {
                            CommonExpr = prevNode;
                            break;
                        }
                    }
                    if (CommonExpr != null)//存在全局公共子表达式
                    {
                        if (CommonExpr.Tags[0].Type != DAGType.Anonymous)//未被选为全局公共子表达式
                        {
                            DAGNode node = new DAGNode(DAGType.Anonymous, GetSN(), block)
                            {
                                Offset = VarSeg.Count
                            };
                            node.CurrentValue = CommonExpr;

                            List<DAGNode> deleteList = new List<DAGNode>();

                            foreach (var i in CommonExpr.Tags)
                            {
                                if (i.Type == DAGType.Temp)
                                {
                                    for (int j = 0; j < CommonExpr.Host.DAG.Count; ++j)
                                    {
                                        if (CommonExpr.Host.DAG[j] == i)
                                        {
                                            CommonExpr.Host.DAG[j] = node;
                                        }
                                        else if (CommonExpr.Host.DAG[j].Type == DAGType.Assign && CommonExpr.Host.DAG[j].Right == i)
                                        {
                                            CommonExpr.Host.DAG[j].Right = node;
                                        }
                                    }
                                    deleteList.Add(i);
                                }
                            }

                            foreach (var i in deleteList)
                            {
                                CommonExpr.Tags.Remove(i);
                            }

                            deleteList.Clear();
                            foreach (var i in nowNode.Tags)
                            {
                                if (i.Type == DAGType.Temp)
                                {
                                    for (int j = 0; j < block.DAG.Count; ++j)
                                    {
                                        if (block.DAG[j] == i)
                                        {
                                            block.DAG[j] = node;
                                        }
                                        else if (block.DAG[j].Type == DAGType.Assign && block.DAG[j].Right == i)
                                        {
                                            block.DAG[j].Right = node;
                                        }
                                    }
                                    deleteList.Add(i);
                                }
                            }
                            //nowNode.Host.ExprSet.Remove(nowNode);
                            foreach (var i in deleteList)
                            {
                                nowNode.Tags.Remove(i);
                            }
                            VarSeg.Add(new QuadrupleNode(QuadrupleType.Var)
                            {
                                Offset = node.Offset,
                                Value = $"#Anonymous_{VarSeg.Count}"
                            });
                            if (CommonExpr.Tags.Count != 0)
                            {
                                CommonExpr.Tags.Add(CommonExpr.Tags[0]);
                                CommonExpr.Tags[0] = node;
                            }
                            else
                            {
                                CommonExpr.Add(node);
                            }
                            //c0可能调用链可能成环，那全局公共子表达式就拉闸
                            //无法解决成环调用，可以进一步推导
                            //暂时搁置
                            //此处调用成环，是由call指令导致的，推导call指令与运算指令位置可得删除哪边的表达式
                            if (block.Next.Contains(CommonExpr.Host) && CommonExpr.Host.Prev.Contains(block)) //说明block用call调用了CommonExpr.Host
                            {
                                var DAG = block.DAG;
                                int addr = CommonExpr.Host.Start;
                                int Loc_call = 0, Loc_expr = 0;
                                foreach (var i in DAG)
                                {
                                    if (i.Type == DAGType.Call && i.Value == addr)
                                    {
                                        Loc_call = i.SN;
                                    }
                                    else if (i == CommonExpr)
                                    {
                                        Loc_expr = i.SN;
                                    }
                                }
                                foreach (var i in block.ExprSet)
                                {
                                    if (i == CommonExpr)
                                    {
                                        Loc_expr = i.SN;
                                        break;
                                    }
                                }
                                if (Loc_call < Loc_expr) //call 在表达式之前发生,删掉调用者中的表达式
                                {
                                    block.DAG.Remove(node);
                                }
                                else
                                {
                                    CommonExpr.Host.DAG.Remove(node);
                                }
                            }
                            else if (CommonExpr.Host.Next.Contains(block) && block.Prev.Contains(CommonExpr.Host)) //说明CommonExpr.Host调用了block
                            {
                                var DAG = CommonExpr.Host.DAG;
                                int addr = block.Start;
                                int Loc_call = 0, Loc_expr = 0;
                                foreach (var i in DAG)
                                {
                                    if (i.Type == DAGType.Call && i.Value == addr)
                                    {
                                        Loc_call = i.SN;
                                        break;
                                    }
                                }
                                foreach (var i in CommonExpr.Host.ExprSet)
                                {
                                    if (i == CommonExpr)
                                    {
                                        Loc_expr = i.SN;
                                        break;
                                    }
                                }
                                if (Loc_call < Loc_expr) //call 在表达式之前发生,删掉调用者中的表达式
                                {
                                    CommonExpr.Host.DAG.Remove(node);
                                }
                                else
                                {
                                    block.DAG.Remove(node);
                                }
                            }
                        }
                    }
                }
            }
        }
        public void LoopOptimization()
        {
            //获取处理完成的自然循环
            //其中可能Loops[j]为Loops[i]的子循环，递归优化Loops[i]后，不需要再优化一次Loops[j]
            List<NaturalLoop> Loops = MergeNaturalLoop();
            foreach (var i in Loops)
            {
                //在循环入口插入一个新的block，从最外层递归优化
                if (!i.Contained)
                {
                    CreateLoopPreHeader(i);
                }
            }
            
            //所有没有被contained的都是最外层自然循环，对其递归优化
            foreach (var i in Loops)
            {
                {
                    InvariantOptimization(i);
                }
            }
            //寻找归纳变量，然后进行强度削减
            DectectedInductionVar = new bool[VarSeg.Count];
            for (int i = 0; i < DectectedInductionVar.Length; ++i)
            {
                DectectedInductionVar[i] = false;
            }
            foreach (var i in Loops)
            {
                if (!i.Contained)
                {
                    InductionOptimization(i);
                }
            }
            foreach (var i in Blocks)
            {
                DeleteUselessTemp(i);
            }
            //如何从Loop重组为Block

        }
        public void GenerateCode()
        {
            //优化的时候需要保证优化完成前的代码跳转指向块
            //产生代码时才能正确指向跳转地址
            //产生代码时每条代码的地址会发生变化，block记录的start也会随之而变
            CodeAddr = 0;
            List<Block> deleteList = new List<Block>();
            Dictionary<int, Block> dict = new Dictionary<int, Block>();
            Blocks.Sort((i, j) =>
            {
                if (i.Start == j.Start)
                {
                    return i.Index.CompareTo(j.Index);
                }
                return i.Start.CompareTo(j.Start);
            });
            foreach (var i in Blocks)
            {
                if (dict.ContainsKey(i.Start))
                {
                    if (dict[i.Start].Index > i.Index)
                    {
                        dict[i.Start] = i;
                    }
                }
                else
                {
                    dict.Add(i.Start, i);//假如有重复的key，说明是循环入口，以及自动声明的块，保证最先声明的块在最前面}
                }
            }
            foreach (var i in Blocks)
            {
                DAG2Code(i, dict);
                if (i.Start > i.End) //可能某个基本块直接被清空，删除
                {
                    deleteList.Add(i);
                }
                else if (CodeSeg[i.End].Type == QuadrupleType.Return)
                {
                    deleteList.Add(i);
                }
            }
            foreach (var i in deleteList)
            {
                Blocks.Remove(i);
            }
            for (int i = 0; i < CodeSeg.Count; ++i)
            {
                if (CodeSeg[i]?.Type == QuadrupleType.Call && CodeSeg[CodeSeg[i].JumpAddr.Start]?.Type == QuadrupleType.Return)
                {
                    CodeSeg[i] = null;
                }
            }
            CompressCodeSeg();
        }

        private void CreateLoopPreHeader(NaturalLoop loop)
        {
            foreach (var i in loop.SubLoop)
            {
                CreateLoopPreHeader(i);
            }
            Block start = Blocks[loop.InnerBlock[0]];
            Block prev = loop.PrevHeader;
            Blocks.Add(prev);
            if (start.Prev.Count == 1 && start.Prev[0].AutoGenerate)//已经有preHeader，说明有内层循环，加在内层循环后面
            {
                //找到内层循环入口和当前循环入口相同的子循环，只可能有一个，否则该多层嵌套了
                NaturalLoop sub = null;
                foreach (var i in loop.SubLoop)
                {
                    if (i.LoopEntrance == loop.LoopEntrance)
                    {
                        sub = i;
                        break;
                    }
                }
                if (sub == null)//有自动生成的块没有相同入口子循环怕是撞鬼
                {
                    throw new Exception();
                }
                prev.Prev.Add(sub.Edge.End);
                prev.Next = sub.Edge.End.Next;
                //那么问题来了 start值是多少,随便设吧，无所谓的
                sub.Edge.End.Next = new List<Block>();
                sub.Edge.End.Next.Add(prev);
                prev.Start = sub.Edge.End.Next.Count == 0 ? sub.Edge.End.End : sub.Edge.End.Next[0].Start;
                foreach (var i in prev.Next)
                {
                    i.Prev.Remove(sub.Edge.End);
                    i.Prev.Add(prev);
                    foreach (var j in i.JumpInsAddr)
                    {
                        j.JumpAddr = prev;
                        prev.JumpInsAddr.Add(j);
                    }
                }
                prev.JumpInsAddr = sub.Edge.End.JumpInsAddr;
                return;
            }
            //外层循环的preHeader应该放到内层循环之后
            prev.Start = start.Start;
            prev.Prev = start.Prev;
            start.Prev = new List<Block>();
            start.Prev.Add(prev);
            foreach (var i in prev.Prev)
            {
                i.Next.Remove(start);
                i.Next.Add(prev);
            }
            prev.Next.Add(start);

            prev.JumpInsAddr = start.JumpInsAddr;
            foreach (var i in prev.JumpInsAddr)
            {
                i.JumpAddr = prev;
            }
        }
        private void InductionOptimization(NaturalLoop loop)
        {
            foreach (var i in loop.SubLoop)
            {
                InductionOptimization(i);
            }
            //归纳变量只有一次赋值
            //获取基本归纳变量
            var dict = loop.AssignDict;
            foreach (var i in loop.InnerBlock)
            {
                var DAG = Blocks[i].DAG;
                for (int k = 0; k < DAG.Count; ++k)
                {
                    var j = DAG[k];
                    if (j.Type == DAGType.Assign)
                    {
                        if (DectectedInductionVar[j.Left.Offset])
                        {
                            continue;
                        }
                        if (dict[j.Left.Offset].Counter < 2)
                        {
                            if (j.Right.Type == DAGType.Sub || j.Right.Type == DAGType.Add)
                            {
                                if (j.Right.Left.Type == DAGType.Num)
                                {
                                    if (j.Right.Right.Type == DAGType.Var && j.Right.Right.Offset == j.Left.Offset)
                                    {
                                        loop.BaseInductionVar.Add(j.Left.Offset, new InductionVar(DAG, k, j, j.Left.Offset, j.Right.Left, j.Type));
                                        DectectedInductionVar[j.Left.Offset] = true;
                                    }
                                }
                                else if (j.Right.Right.Type == DAGType.Num)
                                {
                                    if (j.Right.Left.Type == DAGType.Var && j.Right.Left.Offset == j.Left.Offset)
                                    {
                                        loop.BaseInductionVar.Add(j.Left.Offset, new InductionVar(DAG, k, j, j.Left.Offset, j.Right.Right, j.Type));
                                        DectectedInductionVar[j.Left.Offset] = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            /*
             * 不能删除，可能会出现了2个归纳变量相乘的情况或者归纳变量和变量相乘
            foreach (var i in loop.BaseInductionVar.Keys)
            {
                loop.BaseInductionVar[i].Host.Remove(loop.BaseInductionVar[i].Node);
            }
            */
            //搜寻含有线性计算式的归纳变量，替换，并在preHeader中加入初始赋值
            //对于每一个变量，若无归纳变量相乘或者归纳变量和变量相乘，那么除去该归纳变量
            //没有被除去的归纳变量在更高一级的循环中可能被纳入计算范围，需要设置标志表示被遍历
            //变成归纳变量的变量可以作为归纳变量再次循环，直到没有归纳变量产生，但懒癌发作故这部分不写了
            var baseInductionVar = loop.BaseInductionVar;
            var triples = loop.InductionTriple;
            Stack<DAGNode> stack = new Stack<DAGNode>();
            DAGType lastOP = DAGType.Add;
            foreach (var i in dict.Keys)
            {
                Invariant k = dict[i];
                if (DectectedInductionVar[k.Offset] == false)
                {
                    if (k.Counter < 2) //只能被赋值一次
                    {
                        int target = k.Offset;
                        bool isInduction = true;
                        //这里只处理乘法
                        Triple induction = new Triple(k.Host, k.Addr, k.Node, target, 0);
                        stack.Clear();
                        stack.Push(k.Node.Right);
                        while (stack.Count != 0 && isInduction)
                        {
                            //每次处理掉top的左节点，那么右节点就是一个表达式，继续入栈
                            //Left要求左右为一个归纳变量或者一个循环不变表达式,和一个Num
                            DAGNode top = stack.Pop();
                            top = GetValue(top);
                            if (top.Type == DAGType.Num)
                            {
                                continue;
                            }
                            else if (top.Type == DAGType.Var)
                            {
                                if (baseInductionVar.ContainsKey(top.Offset) == false)
                                {
                                    //在当前块不能存在对该变量的赋值，不然拉闸
                                    foreach (var node in k.Host)
                                    {
                                        if (node.Type == DAGType.Assign && node.Left.Offset == top.Offset)
                                        {
                                            //存在赋值，拉闸
                                            //如果是循环不变表达式则已经被外提，现在的就不可能是循环不变表达式
                                            isInduction = false;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    int incr = (int)baseInductionVar[top.Offset].Incr.Value;
                                    induction.ChangeStep(lastOP == DAGType.Add ? incr : -incr);
                                    induction.AddBaseInduction(top.Left.Offset, lastOP == DAGType.Add ? 1 : -1);
                                }
                            }
                            else if (top.Left.Type == DAGType.Var)
                            {
                                if (baseInductionVar.ContainsKey(top.Left.Offset) == false)
                                {
                                    //在当前块不能存在对该变量的赋值，不然拉闸
                                    foreach (var node in k.Host)
                                    {
                                        if (node.Type == DAGType.Assign && node.Left.Offset == top.Left.Offset)
                                        {
                                            //存在赋值，拉闸
                                            //如果是循环不变表达式则已经被外提，现在的就不可能是循环不变表达式
                                            isInduction = false;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    int incr = (int)baseInductionVar[top.Left.Offset].Incr.Value;
                                    induction.ChangeStep(lastOP == DAGType.Add ? incr : -incr);
                                    induction.AddBaseInduction(top.Left.Offset, lastOP == DAGType.Add ? 1 : -1);
                                }
                            }
                            else if (top.Left.Type == DAGType.Num)
                            {
                                int init = (int)top.Left.Value;
                                induction.ChangeInit(lastOP == DAGType.Add ? init : -init);
                            }
                            else if (top.Left.Type == DAGType.Mul)
                            {
                                DAGNode var, num;
                                DAGNode node = top.Left;
                                if (node.Left.Type == DAGType.Num && node.Right.Type == DAGType.Var)
                                {
                                    var = node.Right;
                                    num = node.Left;
                                }
                                else if (node.Right.Type == DAGType.Num && node.Left.Type == DAGType.Var)
                                {
                                    var = node.Left;
                                    num = node.Right;
                                }
                                else
                                {
                                    isInduction = false;
                                    break;
                                }
                                if (baseInductionVar.ContainsKey(var.Offset) == false)
                                {
                                    //在当前块不能存在对该变量的赋值，不然拉闸
                                    foreach (var j in k.Host)
                                    {
                                        if (j.Type == DAGType.Assign && j.Left.Offset == var.Offset)
                                        {
                                            //存在赋值，拉闸
                                            //如果是循环不变表达式则已经被外提，现在的就不可能是循环不变表达式
                                            isInduction = false;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    int incr = (int)(baseInductionVar[var.Offset].Incr.Value * num.Value);
                                    induction.ChangeStep(lastOP == DAGType.Add ? incr : -incr);
                                    induction.AddBaseInduction(var.Offset, lastOP == DAGType.Add ? (int)num.Value : -1 * (int)num.Value);
                                }
                            }
                            else
                            {
                                isInduction = false;
                                break;
                            }
                            stack.Push(top.Right);
                            lastOP = top.Type;
                        }
                        if (isInduction)
                        {
                            //更改节点，替换计算式为归纳表达式
                            loop.InductionTriple.Add(induction);
                        }
                    }
                }
            }
            foreach (var i in loop.InductionTriple)
            {
                i.OriginNode.Type = DAGType.Add;
                DAGNode right = new DAGNode(DAGType.Add, GetSN(), null);
                i.OriginNode.Right = right;
                right.Left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = i.OriginNode.Offset };
                right.Right = new DAGNode(DAGType.Num, GetSN(), null) { Value = i.Increase };

                DAGNode node = new DAGNode(DAGType.Assign, GetSN(), null);
                DAGNode left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = i.OriginNode.Left.Offset };
                right = new DAGNode(DAGType.Add, GetSN(), null);
                var next = right;
                Stack<Triple.Pair> s = new Stack<Triple.Pair>();
                foreach (var j in i.BaseInductionList)
                {
                    s.Push(j);
                }
                Triple.Pair k;
                if (s.Count == 0)
                {

                }
                else if (s.Count == 1)
                {
                    k = s.Pop();
                    right = new DAGNode(DAGType.Mul, GetSN(), null)
                    {
                        Left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = k.Offset },
                        Right = new DAGNode(DAGType.Num, GetSN(), null) { Value = k.Coefficient }
                    };
                }
                else
                {
                    k = s.Pop();
                    right.Left = new DAGNode(DAGType.Mul, GetSN(), null)
                    {
                        Left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = k.Offset },
                        Right = new DAGNode(DAGType.Num, GetSN(), null) { Value = k.Coefficient }
                    };
                    while (s.Count > 1)
                    {
                        k = s.Pop();
                        next = next.Right;
                        next.Left = new DAGNode(DAGType.Mul, GetSN(), null)
                        {
                            Left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = k.Offset },
                            Right = new DAGNode(DAGType.Num, GetSN(), null) { Value = k.Coefficient }
                        };
                    }
                    k = s.Pop();
                    next.Right = new DAGNode(DAGType.Mul, GetSN(), null)
                    {
                        Left = new DAGNode(DAGType.Var, GetSN(), null) { Offset = k.Offset },
                        Right = new DAGNode(DAGType.Num, GetSN(), null) { Value = k.Coefficient }
                    };
                }
                loop.PrevHeader.DAG.Add(node);
            }
        }
        private void InvariantOptimization(NaturalLoop loop)
        {
            Dictionary<int, Invariant> dict = new Dictionary<int, Invariant>();
            foreach (var i in loop.SubLoop)
            {
                InvariantOptimization(i);
                GetAssignFromPreHeader(i, dict);
            }
            //若存在一个表达式 i = x op y，有x y在循环内值不变，i不被重赋值，则可以外提
            //由于没有continue,break,goto等循环出口只有一个

            //累计赋值次数，key是offset
            GetAssignToDict(loop, dict);
            loop.PrevHeader.Index = GetSN();
            //此时所有赋值次数为1的已被筛选
            //确保左右运算节点未被赋值
            List<Invariant> InvariantExpr = new List<Invariant>();
            Stack<DAGNode> stack = new Stack<DAGNode>();
            foreach (var i in dict.Keys)
            {
                if (dict[i].Counter > 1)
                {
                    continue;
                }
                stack.Clear();
                var node = dict[i].Node.Right;
                stack.Push(node);
                bool ok = true;
                while (stack.Count != 0)
                {
                    var top = stack.Pop();
                    if (top.Type == DAGType.Temp)
                    {
                        top = GetValue(top);
                    }
                    if (IsExpressionNode(top))
                    {
                        stack.Push(top.Left);
                        stack.Push(top.Right);
                    }
                    else if (top.Type == DAGType.Num)
                    {
                        continue;
                    }
                    else
                    {
                        if (top.Offset != dict[i].Offset)
                        {
                            if (dict.ContainsKey(top.Offset))
                            {
                                ok = false;
                                break;
                            }
                        }
                        else
                        {
                            ok = false;
                            break;
                        }
                    }
                }
                if (ok)
                {
                    InvariantExpr.Add(dict[i]);
                }
            }
            //InvariantExpr保存了所有的不变表达式，外提
            //只保存了assign的，涉及到的表达式都要外提
            //外提的表达式可能是引用的和被引用的
            //只需要移除原来DAG的赋值节点，其他的引用会被DeleteUselessTemp()清除
            foreach (var i in InvariantExpr)
            {
                MoveNode(i.Node, loop.PrevHeader);//什么都不做，在生成的时候对PreHeader中的DAG特殊处理
                i.Host.Remove(i.Node);
                loop.PrevHeader.DAG.Add(i.Node);
            }
            loop.AssignDict = dict;
        }
        private void MoveNode(DAGNode node, Block Target)
        {
            /*
            if (node.Type == DAGType.Var)
            {
                var t = FindMatchNode(node, Target.DAG);
                if (t == null)
                {
                    var result = new DAGNode(DAGType.Var, GetSN(), Target);
                    node.Add(result);
                    return result;
                }
                return t;
            }
            else if (node.Type == DAGType.Num)
            {
                var t = FindMatchNode(node, Target.DAG);
                if (t == null)
                {
                    var result = new DAGNode(DAGType.Num, GetSN(), Target) { Value = node.Value };
                    node.Add(result);
                    return result;
                }
                return t;
            }
            else
            {
                node.Left = MoveNode(node.Left, Target);
                node.Right = MoveNode(node.Right, Target);
                //这里的临时变量名可能重复！

                foreach (var i in node.Tags)
                {

                }
            }
            */
        }
        private void GetAssignToDict(NaturalLoop loop, Dictionary<int, Invariant> dict)
        {
            for (int i = 0; i < loop.InnerBlock.Count; ++i)
            {
                var block = Blocks[loop.InnerBlock[i]];
                for (int j = 0; j < block.DAG.Count; ++j)
                {
                    var node = block.DAG[j];
                    if (node.Type == DAGType.Assign)
                    {
                        if (dict.ContainsKey(node.Left.Offset) == false)
                        {
                            dict.Add(node.Left.Offset, new Invariant(block.DAG, j, node, node.Left.Offset));
                        }
                        else
                        {
                            dict[node.Left.Offset].Counter = dict[node.Left.Offset].Counter + 1;
                        }
                    }
                }
            }
        }
        private void GetAssignFromPreHeader(NaturalLoop loop, Dictionary<int, Invariant> dict)
        {
            foreach (var node in loop.PrevHeader.DAG)
            {
                if (node.Type == DAGType.Assign)
                {
                    if (dict.ContainsKey(node.Left.Offset) == false)
                    {
                        dict.Add(node.Left.Offset, new Invariant(loop.PrevHeader.DAG, -1, node, node.Left.Offset));
                    }
                    else
                    {
                        dict[node.Left.Offset].Counter = dict[node.Left.Offset].Counter + 1;
                    }
                }
            }
        }
        private List<NaturalLoop> MergeNaturalLoop()
        {
            //O(n^3 lgn)复杂度警告
            List<NaturalLoop> Loops = new List<NaturalLoop>();
            FindNaturalLoop(Loops);

            //Loops排序，从短到长排序，保证每个循环最多只被包含一次
            //若多个循环同时包含 i,那么他们之间关系是包含或者独立或者交叉，独立是不可能，交叉不符合循环定义
            Func<NaturalLoop, NaturalLoop, int> cmp = (a, b) =>
            {
                return a.InnerBlock.Count.CompareTo(b.InnerBlock.Count);
            };
            Loops.Sort(new Comparison<NaturalLoop>(cmp));

            bool[] v = new bool[Loops.Count];
            //这里的循环顺序很重要
            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = false;
            }
            for (int i = 1; i < Loops.Count - 1; ++i)
            {
                if (v[i])
                {
                    continue;
                }
                for (int j = 0; j < i; ++j)
                {
                    if (Loops[j].Contained)
                    {
                        continue;
                    }
                    int result = Loops[i].CompareTo(Loops[j]);
                    switch (result)
                    {
                        case -2:
                            if (Loops[j].SubLoop.Contains(Loops[i]) == false)
                            {
                                Loops[j].SubLoop.Add(Loops[i]);
                                Loops[i].Contained = true;
                            }
                            break;
                        case -1:
                            //i真包含j
                            if (Loops[i].SubLoop.Contains(Loops[j]) == false)
                            {
                                Loops[i].SubLoop.Add(Loops[j]);
                                Loops[j].Contained = true;
                            }
                            break;
                        case 0:
                            //合并循环
                            //合并后J就不用再扫描了
                            Loops[i].SubLoop = Loops[i].SubLoop.Union(Loops[j].SubLoop);

                            //可能导致循环入口被排到后面去
                            //Note
                            Loops[i].InnerBlock = Loops[i].InnerBlock.Union(Loops[j].InnerBlock);

                            Loops[j] = Loops[i];
                            v[j] = true;
                            break;
                        case 1:
                            //i真包含j
                            if (Loops[i].SubLoop.Contains(Loops[j]) == false)
                            {
                                Loops[i].SubLoop.Add(Loops[j]);
                                Loops[j].Contained = true;
                            }
                            break;
                        case 2:
                            if (Loops[j].SubLoop.Contains(Loops[i]) == false)
                            {
                                Loops[j].SubLoop.Add(Loops[i]);
                                Loops[i].Contained = true;
                            }
                            break;
                        case 3:
                            //独立循环 不管
                            break;
                        case 4:
                            throw new Exception("归并自然循环时未知错误发生");


                    }

                }
            }
            //删除重复的
            return Loops.Distinct();
        }
        private void FindNaturalLoop(List<NaturalLoop> Loops)
        {
            //从EntranceBlock开始
            //使用强连通分量找出所有自然循环
            List<BackEdge> backEdges = new List<BackEdge>();
            for (int i = 0; i < Blocks.Count; ++i)
            {
                Blocks[i].Addr = i;
            }
            GetBackEdge(backEdges);
            bool[] v = new bool[Blocks.Count];
            Stack<Block> stack = new Stack<Block>();
            foreach (var i in backEdges)
            {
                for (int j = 0; j < v.Length; ++j)
                {
                    v[j] = false;
                }
                v[i.End.Addr] = true;
                NaturalLoop loop = new NaturalLoop(i)
                {
                    LoopEntrance = i.Start.Addr
                };
                //DFS反向流图，获取循环节点
                stack.Clear();
                stack.Push(i.Start);
                loop.InnerBlock.Add(i.End.Addr);
                while (stack.Count != 0)
                {
                    var top = stack.Pop();
                    if (v[top.Addr])
                    {
                        continue;
                    }
                    v[top.Addr] = true;
                    loop.InnerBlock.Add(top.Addr);
                    foreach (var j in top.Prev) //反向边
                    {
                        stack.Push(j);
                    }
                }
                Loops.Add(loop);
            }
        }
        /// <summary>
        /// DFS控制流图，生成遍历顺序，并获取回边
        /// </summary>
        /// <param name="backEdges"></param>
        private void GetBackEdge(List<BackEdge> backEdges)
        {
            bool[] v = new bool[Blocks.Count];
            for (int i = 0; i < v.Length; ++i)
            {
                v[i] = false;
            }
            Stack<Block> stack = new Stack<Block>();
            stack.Push(EntranceBlock);
            int index = 0;
            while (stack.Count != 0)
            {
                var top = stack.Pop();
                if (v[top.Addr])
                {
                    continue;
                }
                v[top.Addr] = true;
                top.Index = index++;
                foreach (var i in top.Next)
                {
                    stack.Push(i);
                    if (i.Index != -1 && i.Index <= top.Index) //跳转到自己也算循环
                    {
                        backEdges.Add(new BackEdge(top, i));
                    }
                }
            }
        }
        private bool ExistEdge(Block b1, Block b2)
        {
            return b1.Next.Contains(b2);
        }
        private bool ExistReverseEdge(Block b1, Block b2)
        {
            return b1.Prev.Contains(b2);
        }
        private void CompressCodeSeg()
        {
            RelocateJumpAddrToAddr();
            //删除无用，然后重新进行代码划分，删除死代码
            CodeSeg.RemoveRange(CodeAddr, CodeSeg.Count - CodeAddr);
            DivideBlock();
            RemoveDeadCode();
            //代码之间会出现空代码，对Block:Start&End重定位
            RemoveBlank();
        }
        private void RemoveBlank()
        {
            CodeAddr = 0;
            RelocateJumpAddrToBlock();
            foreach (var block in Blocks)
            {
                if (CodeAddr != block.Start)//该基本块与前一个基本块间有空隙
                {
                    int Step = block.Start - CodeAddr;
                    for (int i = block.Start; i <= block.End; ++i)
                    {
                        if (CodeSeg[i] != null)
                        {
                            CodeSeg[CodeAddr++] = CodeSeg[i];
                        }
                    }
                    block.Start = block.Start - Step;
                    block.End = CodeAddr - 1;
                }
                CodeAddr = block.End + 1;//下一个待填写位置
            }
            CodeSeg.RemoveRange(CodeAddr, CodeSeg.Count - CodeAddr);
            RelocateJumpAddrToAddr();
        }
        private void IterativeAliveVarAnalysis()
        {
            foreach (var block in Blocks)
            {
                if (block.AutoGenerate)
                {
                    continue;
                }
                foreach (var node in block.ExprSet)
                {
                    if (node.Left.Type == DAGType.Var)
                    {
                        if (block.InputVar.Contains(node.Left) == false)
                        {
                            block.InputVar.Add(node.Left);
                        }
                    }
                    if (node.Right.Type == DAGType.Var)
                    {
                        if (block.InputVar.Contains(node.Right) == false)
                        {
                            block.InputVar.Add(node.Right);
                        }
                    }
                }
                foreach (var node in block.DAG.ToList())//DAG的拷贝，不然引发集合被修改无法枚举异常
                {
                    if (node.Type == DAGType.Write)
                    {
                        var list = node.list;
                        foreach (var v in list)
                        {
                            QuadrupleNode Var = v as QuadrupleNode;
                            DAGNode tmp = GetNode(Var, block.DAG, block);
                            if (block.InputVar.Contains(tmp) == false/* && tmp.CurrentValue == tmp*/)
                            {
                                block.InputVar.Add(tmp);
                            }
                        }
                    }
                    else if (node.Type == DAGType.Var && node.CurrentValue == node)
                    {
                        if (block.InputVar.Contains(node) == false)
                        {
                            block.InputVar.Add(node);
                        }
                    }
                }
            }
            foreach (var block in Blocks)
            {
                block.ActiveVar = block.InputVar;
            }
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var block in Blocks)
                {
                    var oldActiveVar = block.ActiveVar;
                    foreach (var i in block.Next)
                    {
                        block.ActiveVar = block.ActiveVar.Union(i.ActiveVar);
                    }
                    Func<DAGNode, DAGNode, int> cmp = (n1, n2) =>
                    {
                        return n1.Offset.CompareTo(n2.Offset);
                    };
                    block.ActiveVar.ToList().Sort(new Comparison<DAGNode>(cmp));
                    oldActiveVar.ToList().Sort(new Comparison<DAGNode>(cmp));

                    Func<DAGNode, DAGNode, int> cmp1 = (n1, n2) =>
                    {
                        return n1.SN.CompareTo(n2.SN);
                    };
                    if (!block.ActiveVar.ElementEqual(oldActiveVar, cmp1))
                    {
                        changed = true;
                    }
                    //比较序列是否改变相等
                }
            }
        }
        private void IterativeAvailableExpressionAnalysis()
        {
            foreach (var block in Blocks)
            {
                List<DAGNode> deleteList = null;
                foreach (var node in block.DAG)
                {
                    if (node.Type == DAGType.Assign)
                    {
                        int offset = node.Left.Offset, SN = node.SN;
                        deleteList = new List<DAGNode>();
                        foreach (var i in block.ExprSet)
                        {
                            if (i.Left.Offset == offset || i.Right.Offset == offset)
                            {
                                if (i.SN < SN)
                                {
                                    if (!deleteList.Contains(i))
                                    {
                                        deleteList.Add(i);
                                    }
                                }
                            }
                        }
                    }
                }
                if (deleteList != null)
                {
                    block.AvailableExpr = block.ExprSet.Except(deleteList);
                }
            }
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var block in Blocks)
                {
                    var oldExprSet = block.OutAvailableExpr;
                    if (block.Prev.Count != 0)
                    {
                        block.InAvailableExpr = block.Prev[0].OutAvailableExpr;
                        for (int i = 1; i < block.Prev.Count; ++i)
                        {
                            block.InAvailableExpr = block.InAvailableExpr.Intersect(block.Prev[i].OutAvailableExpr);
                            if (block.InAvailableExpr.Count == 0)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        block.InAvailableExpr = new List<DAGNode>();
                    }
                    Func<DAGNode, DAGNode, int> cmp = (n1, n2) =>
                    {
                        return n1.SN.CompareTo(n2.SN);
                    };
                    block.OutAvailableExpr = block.InAvailableExpr.Union(block.AvailableExpr);
                    if (!oldExprSet.ElementEqual(block.OutAvailableExpr, cmp))
                    {
                        changed = true;
                    }
                }
            }
        }
        private void RelocateJumpAddrToAddr()
        {
            int JumpValue = Convert.ToInt32(QuadrupleType.JMP);
            foreach (var i in CodeSeg)
            {
                if (i == null)
                {
                    continue;
                }
                if (Convert.ToInt32(i.Type) <= JumpValue)
                {
                    i.Result = i.JumpAddr.Start;
                }
                else if (i.Type == QuadrupleType.Call)
                {
                    i.Result = i.JumpAddr.Start;
                }
            }
        }
        private void RemoveDeadCode()
        {
            foreach (var i in Blocks)
            {
                for (int k = i.Start; k <= i.End; ++k)
                {
                    visited[k] = true;
                }
            }
            for (int i = 0; i < CodeSeg.Count; ++i)
            {
                if (visited[i] == false)
                {
                    CodeSeg[i] = null;
                }
            }
        }
        private DAGNode GetValue(DAGNode node)
        {
            if (node.Type == DAGType.Temp) //必为运算结果
            {
                var result = node.CurrentValue;
                if (result.Type != DAGType.Temp)
                {
                    return GetValue(result);
                }
                if (result.Offset != node.Offset)
                {
                    return GetValue(result);
                }
                return node;
            }
            else if (node.Type == DAGType.Var)
            {
                if (node.CurrentValue.Type == DAGType.Temp)
                {
                    return GetValue(node.CurrentValue);
                }
                return node.CurrentValue;
            }
            else if (node.Type == DAGType.Num)
            {
                return node;
            }
            else//为表达式
            {
                return node;
            }

        }
        private int CodeAddr;
        private int Temp;

        private void GenerateAutoGeneratedCode(DAGNode node)
        {
            GenerateAutoGeneratedCode(node.Right);
            if (node.Type == DAGType.Assign)
            {
                CodeSeg[CodeAddr++] = new QuadrupleNode(QuadrupleType.Assign)
                {
                    Arg1 = new QuadrupleNode(QuadrupleType.Var)
                    {
                        Offset = node.Left.Offset
                    }
                };
                if (node.Right.Type == DAGType.Var)
                {
                    CodeSeg[CodeAddr - 1].Arg2 = VarSeg[node.Right.Offset];
                }
                else if (node.Right.Type == DAGType.Num)
                {
                    CodeSeg[CodeAddr - 1].Arg2 = $"#{node.Right.Value}";
                }
                else
                {
                    CodeSeg[CodeAddr - 1].Arg2 = Temp;
                }
            }
            else //说明是表达式
            {
                QuadrupleType type;
                switch (node.Type)
                {
                    case DAGType.Sub:
                        type = QuadrupleType.Sub;
                        break;
                    case DAGType.Add:
                        type = QuadrupleType.Add;
                        break;
                    case DAGType.Mul:
                        type = QuadrupleType.Mul;
                        break;
                    case DAGType.Div:
                        type = QuadrupleType.Div;
                        break;
                    default:
                        throw new Exception();
                }
                QuadrupleNode code = new QuadrupleNode(type);
                if (node.Left.Type == DAGType.Var)
                {
                    code.Arg1 = VarSeg[node.Left.Offset];
                }
                else if (node.Left.Type == DAGType.Num)
                {
                    code.Arg1 = $"#{node.Left.Value}";
                }
                else
                {
                    GenerateAutoGeneratedCode(node.Left);
                    code.Arg1 = Temp;
                }
                if (node.Right.Type == DAGType.Var)
                {
                    code.Arg2 = VarSeg[node.Right.Offset];
                }
                else if (node.Right.Type == DAGType.Num)
                {
                    code.Arg2 = $"#{node.Right.Value}";
                }
                else
                {
                    GenerateAutoGeneratedCode(node.Right);
                    code.Arg2 = Temp;
                }
                code.Result = ++Temp;
            }
        }
        private void DAG2Code(Block block, Dictionary<int, Block> dict)
        {
            //压缩代码空间，去掉空白代码部分(被优化掉)
            //将DAG翻译为四地址码，最后可能的return和jump不管，优化代码只会少不会多，所以保留原位即可
            //根据sn的值来生成代码,在DAG生成代码前可以先进行全局优化
            if (block.AutoGenerate)
            {
                //只有Assign类型，但要对每个表达式自展开
                block.Start = CodeAddr;
                foreach (var i in block.DAG)
                {
                    if (i.Type != DAGType.Assign)
                    {
                        throw new Exception();
                    }
                    GenerateAutoGeneratedCode(i);
                    Temp = -1;
                }
                block.End = CodeAddr - 1;
                return;
            }
            if (block.Start > block.End)
            {
                return;
            }
            List<DAGNode> Nodes = block.DAG;
            // Nodes.Sort((t1, t2) => { return t1.SN.CompareTo(t2.SN); });

            block.Start = CodeAddr;
            for (int i = 0; i < Nodes.Count; ++i)
            {
                DAGNode node = Nodes[i];
                if (node.Type == DAGType.Num)
                {
                    continue;
                }
                else if (node.Type == DAGType.Var)
                {
                    continue;
                }
                else if (node.Type == DAGType.Anonymous)
                {
                    QuadrupleType type;
                    switch (node.CurrentValue.Type)
                    {
                        case DAGType.Add:
                            type = QuadrupleType.Add;
                            break;
                        case DAGType.Mul:
                            type = QuadrupleType.Mul;
                            break;
                        case DAGType.Div:
                            type = QuadrupleType.Div;
                            break;
                        case DAGType.Sub:
                            type = QuadrupleType.Sub;
                            break;
                        default:
                            throw new Exception("不存在预期的表达式");
                    }
                    QuadrupleNode code = new QuadrupleNode(type)
                    {
                        Arg1 = ConvertNode(node.CurrentValue.Left),
                        Arg2 = ConvertNode(node.CurrentValue.Right),
                        Result = -node.Offset
                    };
                    CodeSeg[CodeAddr++] = code;
                    continue;
                }
                else if (node.Type == DAGType.Temp) //这里只处理中间运算节点
                {
                    if (node.CurrentValue.Type == DAGType.Var || node.CurrentValue.Type == DAGType.Anonymous)
                    {
                        continue;
                    }
                    else if (node.CurrentValue.Type == DAGType.Num)
                    {
                        continue;
                    }
                    else
                    {
                        QuadrupleType type = QuadrupleType.Call;
                        switch (GetValue(node).Type)
                        {
                            case DAGType.Add:
                                type = QuadrupleType.Add;
                                break;
                            case DAGType.Mul:
                                type = QuadrupleType.Mul;
                                break;
                            case DAGType.Div:
                                type = QuadrupleType.Div;
                                break;
                            case DAGType.Sub:
                                type = QuadrupleType.Sub;
                                break;
                            default:
                                throw new Exception("不存在预期的表达式");
                        }
                        QuadrupleNode code = new QuadrupleNode(type)
                        {
                            Arg1 = ConvertNode(GetValue(node).Left),
                            Arg2 = ConvertNode(GetValue(node).Right),
                            Result = (int)node.Value
                        };
                        CodeSeg[CodeAddr++] = code;
                        continue;
                    }
                }
                else if (node.Type == DAGType.Assign)
                {
                    QuadrupleNode codenode = new QuadrupleNode(QuadrupleType.Assign);
                    if (node.Left.Type != DAGType.Var)
                    {
                        throw new Exception("只有变量能被赋值");
                    }
                    else
                    {
                        codenode.Arg1 = VarSeg[node.Left.Offset];
                    }
                    codenode.Arg2 = ConvertNode(node.Right);
                    CodeSeg[CodeAddr++] = codenode;
                    continue;
                }
                else if (node.Type == DAGType.Read)
                {
                    QuadrupleNode codenode = new QuadrupleNode(QuadrupleType.Read)
                    {
                        Arg1 = node.list
                    };
                    CodeSeg[CodeAddr++] = codenode;
                }
                else if (node.Type == DAGType.Write)
                {
                    /* for (int k = 0; k < node.list.Count; ++k)
                     {
                         DAGNode n = GetNode(node.list[k], block.DAG, block);
                         if (n.CurrentValue != n)
                         {
                             var temp = GetValue(n);
                             if (temp.Type == DAGType.Num)
                             {
                                 node.list[k] = ConvertNode(temp);
                             }
                             else if (temp.Type == DAGType.Var)
                             {
                                 node.list[k] = ConvertNode(temp);
                             }
                             else
                             {
                                 if (temp.Type == DAGType.Temp)
                                 {
                                     throw new Exception();
                                 }
                                 foreach (var e in temp.Tags)
                                 {
                                     if (e.Type == DAGType.Temp)
                                     {
                                         node.list[k] = ConvertNode(e);
                                     }
                                 }
                             }
                         }
                     }*/
                    QuadrupleNode codenode = new QuadrupleNode(QuadrupleType.Write)
                    {
                        Arg1 = node.list
                    };
                    CodeSeg[CodeAddr++] = codenode;
                }
                else if (node.Type == DAGType.Call)
                {
                    QuadrupleNode codenode = new QuadrupleNode(QuadrupleType.Call)
                    {
                        JumpAddr = dict[(int)node.Value]
                    };
                    CodeSeg[CodeAddr++] = codenode;
                }
                else
                {
                    throw new Exception("Unexpected Error");
                }
            }
            int end = CodeAddr - 1;//CodeAddr指向下一个待填的
            //处理代码跳转部分
            if (CodeAddr <= block.End && !NotJumpOrReturn(block.End))
            {
                CodeSeg[CodeAddr] = CodeSeg[block.End];
                if (IsConditionJump(CodeAddr))
                {
                    QuadrupleNode node = CodeSeg[CodeAddr];

                    var left = GetNode(node.Arg1, Nodes, block);
                    var right = GetNode(node.Arg2, Nodes, block);
                    var lv = GetValue(left);
                    var rv = GetValue(right);
                    if (lv.Type == DAGType.Num)
                    {
                        node.Arg1 = $"#{lv.Value}";
                    }
                    if (rv.Type == DAGType.Num)
                    {
                        node.Arg2 = $"#{rv.Value}";
                    }
                }
                if (IsConditionJump(CodeAddr) || CodeSeg[block.End].Type == QuadrupleType.JMP)
                {
                    CodeSeg[block.End].JumpAddr = dict[(int)CodeSeg[block.End].Result];
                }
                end = CodeAddr;
                CodeAddr++;
            }
            //空白区域置null
            for (int i = CodeAddr; i <= block.End; ++i)
            {
                CodeSeg[i] = null;
            }
            block.End = end;
        }
        private DAGNode GetNode(object n, List<DAGNode> Nodes, Block block)
        {
            //返回的节点为Temp,Temp首先会被赋值,CurrentValue必不为空
            //返回的节点为Var ,Var不一定会被赋值,CurrentValue可能为空
            //返回节点为Num
            if (n is int)//临时变量
            {
                DAGNode tmp = new DAGNode(DAGType.Temp, GetSN(), block)
                {
                    Value = Convert.ToInt32(n)
                };
                DAGNode match = FindMatchNode(tmp, Nodes);
                if (match == null)
                {
                    Nodes.Add(tmp);
                    return tmp;
                }
                return match;
            }
            else if (n is string) //立即数
            {
                DAGNode tmp = new DAGNode(DAGType.Num, GetSN(), block)
                {
                    Value = Convert.ToInt64(((string)n).Substring(1)) //转为long防止计算时溢出
                };
                DAGNode match = FindMatchNode(tmp, Nodes);
                if (match == null)
                {
                    Nodes.Add(tmp);
                    return tmp;
                }
                match.CurrentValue.Add(tmp); //代表引用次数，为1则删除
                return match;
            }
            else if (n is QuadrupleNode)
            {
                DAGNode tmp = new DAGNode(DAGType.Var, GetSN(), block)
                {
                    Offset = ((QuadrupleNode)n).Offset
                };
                DAGNode match = FindMatchNode(tmp, Nodes);
                if (match == null)
                {
                    Nodes.Add(tmp);
                    tmp.CurrentValue = tmp;
                    return tmp;
                }
                return match;
            }
            return null;
        }
        private void DeleteUselessTemp(Block block)
        {
            //消除死代码，针对临时变量
            //任意一个非变量节点如果Tags中不存在变量，即没有附加在活跃的变量之上，就可以除去
            //局部除去死代码只能去除临时变量，去除变量需要扫描全部DAG，处理方式见400行附近多行注释的代码区
            /*
             *   节点类型：
             *       Num ：CurrentValue必然是Num本身，Tags包含Var，Temp类型，Temp只有一个，不然违反无公共子表达式的定义
             *       Temp:  CurrrentValue为Num,运算节点，Var
             *       Var：   CurrentValue为运算节点，Num
             *   创建好DAG后，从后向前扫描(方向无所谓，在全局扫描时才有必要从后向前)
             *   跳转指令指向的临时变量设置为Active
             *   Var指向的运算节点，将其Tag中的Temp变量设置为活跃
             *   然后递归表达式，将其中各个Tag设置为活跃
             *   在局部删除时暂定所有变量为ACTIVE
             */
            List<DAGNode> Nodes = block.DAG;
            Queue<DAGNode> ActiveExpr = new Queue<DAGNode>();
            foreach (var i in Nodes)
            {
                if (i.Type == DAGType.Var)
                {
                    var value = GetValue(i);
                    if (IsExpressionNode(value))
                    {
                        ActiveExpr.Enqueue(value);
                    }
                }
                else if (i.Type == DAGType.Assign)
                {
                    if (i.Right.Type == DAGType.Temp)
                    {
                        i.Right.Active = true;
                        var value = GetValue(i.Right);
                        if (IsExpressionNode(value))
                        {
                            ActiveExpr.Enqueue(value);
                        }
                    }
                }
                else if (i.Type == DAGType.Write)
                {
                    var list = i.list;
                    foreach (var n in list)
                    {
                        var var = GetNode(n, block.DAG, block);
                        if (IsExpressionNode(var.CurrentValue))
                        {
                            ActiveExpr.Enqueue(var.CurrentValue);
                        }
                    }
                }
            }
            //若最后为Jump，将Jump涉及到的Temp设置为Active
            if (!block.AutoGenerate && IsConditionJump(block.End)) //表达式可能恒为真
            {
                QuadrupleNode node = CodeSeg[block.End];
                DAGNode left = GetNode(node.Arg1, Nodes, block), right = GetNode(node.Arg2, Nodes, block);
                if (GetValue(left) == GetValue(right))
                {
                    switch (node.Type)
                    {
                        case QuadrupleType.JE:
                        case QuadrupleType.JGE:
                        case QuadrupleType.JLE:
                            node.Type = QuadrupleType.JMP;
                            node.Arg1 = null;
                            node.Arg2 = null;
                            break;
                        case QuadrupleType.JL:
                        case QuadrupleType.JNE:
                        case QuadrupleType.JG:
                            CodeSeg[block.End] = null;
                            block.End--;
                            if (block.Start < block.End)
                            {
                                return;
                            }
                            break;
                    }
                }
                else
                {
                    if (left.CurrentValue.Type == DAGType.Num && right.CurrentValue.Type == DAGType.Num)
                    {
                        switch (node.Type)
                        {
                            case QuadrupleType.JE:
                            case QuadrupleType.JGE:
                            case QuadrupleType.JLE:
                                CodeSeg[block.End] = null;
                                block.End--;
                                if (block.Start < block.End)
                                {
                                    return;
                                }
                                break;
                            case QuadrupleType.JL:
                            case QuadrupleType.JNE:
                            case QuadrupleType.JG:
                                node.Type = QuadrupleType.JMP;
                                node.Arg1 = null;
                                node.Arg2 = null;
                                break;
                        }
                    }
                    else
                    {
                        if (left.Type == DAGType.Temp)
                        {
                            left.Active = true;
                            var value = GetValue(left);
                            if (IsExpressionNode(value))
                            {
                                ActiveExpr.Enqueue(value);
                            }
                        }
                        if (right.Type == DAGType.Temp)
                        {
                            right.Active = true;
                            var value = GetValue(right);
                            if (IsExpressionNode(value))
                            {
                                ActiveExpr.Enqueue(value);
                            }
                        }
                    }
                }
            }
            //表达式涉及到的所有Temp设置为Active
            while (ActiveExpr.Count != 0)
            {
                var node = ActiveExpr.Dequeue();
                if (node.Left.Type == DAGType.Temp)
                {
                    node.Left.Active = true;
                    var value = GetValue(node.Left);
                    if (IsExpressionNode(value))
                    {
                        foreach (var i in value.Tags)
                        {
                            if (i.Type == DAGType.Temp)
                            {
                                i.Active = true;
                            }
                        }
                        ActiveExpr.Enqueue(value);
                    }
                }
                else if (IsExpressionNode(node.Left))
                {
                    foreach (var i in node.Left.Tags)
                    {
                        if (i.Type == DAGType.Temp)
                        {
                            i.Active = true;
                            break;//多个临时变量指向同一个说明存在公共子表达式，保留第一个
                        }
                    }
                    ActiveExpr.Enqueue(node.Left);
                }
                if (node.Right.Type == DAGType.Temp)
                {
                    var value = GetValue(node.Right);
                    node.Right.Active = true;
                    if (IsExpressionNode(value))
                    {
                        foreach (var i in value.Tags)
                        {
                            if (i.Type == DAGType.Temp)
                            {
                                i.Active = true;
                            }
                        }
                        ActiveExpr.Enqueue(value);
                    }
                }
                else if (IsExpressionNode(node.Right))
                {
                    foreach (var i in node.Right.Tags)
                    {
                        if (i.Type == DAGType.Temp)
                        {
                            i.Active = true;
                            break;//多个临时变量指向同一个说明存在公共子表达式，保留第一个
                        }
                    }
                    ActiveExpr.Enqueue(node.Right);
                }
            }
            //删除无用代码，包括将var,num 通过mov移动至临时变量等
            List<DAGNode> deleteList = new List<DAGNode>();
            foreach (var i in Nodes)
            {
                if (i.Type == DAGType.Temp && i.Active == false)
                {
                    deleteList.Add(i);
                }
            }
            foreach (var i in deleteList)
            {
                Nodes.Remove(i);
            }
        }
        private object ConvertNode(DAGNode n)
        {
            if (n.Type == DAGType.Num)
            {
                return $"#{n.Value}";
            }
            else if (n.Type == DAGType.Var)//不应该出现，变量不能直接累加到临时变量，应该首先MOV到临时变量,若出现则查找是否以及赋值给临时变量
            {
                foreach (var k in n.Tags)
                {
                    if (k.Type == DAGType.Temp)
                    {
                        return (int)k.Value;
                    }
                }
                return VarSeg[n.Offset];
            }
            else if (n.Type == DAGType.Temp) //尽量用常数替换
            {
                if (n.CurrentValue.Type == DAGType.Num)
                {
                    return "#" + n.CurrentValue.Value;
                }
                return (int)n.Value;
            }
            else if (n.Type == DAGType.Anonymous)
            {
                return VarSeg[n.Offset];
            }
            else//内部运算节点，从其Tag中找到一个临时变量，运算值在之前必然赋值给了一个临时变量
            {
                int temp = -1;
                foreach (var k in n.Tags)
                {
                    if (k.Type == DAGType.Temp)
                    {
                        return (int)k.Value;
                    }
                }
                if (temp == -1)
                {
                    throw new Exception("DAG生成代码过程中遇到了错误");
                }
            }
            return null;
        }
        private bool IsExpressionNode(DAGNode node)
        {
            if (node == null)
            {
                return false;
            }
            if (node.Type == DAGType.Sub || node.Type == DAGType.Mul || node.Type == DAGType.Div || node.Type == DAGType.Add)
            {
                return true;
            }
            return false;
        }
        private void ResetSN()
        {
            SerialNumber = 0;
        }
        private int GetSN()
        {
            return SerialNumber++;
        }
        private DAGNode FindMatchNode(DAGNode node, List<DAGNode> list)
        {
            foreach (var i in list)
            {
                if (node.Equals(i))
                {
                    return i;
                }
            }
            return null;
        }
        /// <summary>
        /// 会导致基本块的重划分
        /// </summary>
        private void DivideBlock()
        {
            //划分基本块，同时创建好控制流中基本块的前驱后继
            //前驱后继也包括call跳转
            Blocks.Clear();
            Queue<int> EntranceQueue = new Queue<int>();
            FindEntranceStatement(EntranceQueue);
            while (EntranceQueue.Count != 0)
            {
                int index = EntranceQueue.Dequeue();
                Block block = new Block
                {
                    Start = index
                };
                if (Blocks.Count == 0)
                {
                    EntranceBlock = block;
                }
                if (!NotJumpOrReturn(index))
                {
                    block.End = index;
                    Blocks.Add(block);
                    continue;
                }
                index++;
                while (index < CodeSeg.Count && NotJumpOrReturn(index) && !visited[index])
                {
                    ++index;
                }
                if (index >= CodeSeg.Count)
                {
                    block.End = CodeSeg.Count - 1;
                }
                else if (visited[index])
                {
                    if (index == block.Start)
                    {
                        block.End = index;
                    }
                    else
                    {
                        block.End = index - 1;
                    }
                }
                else //跳转或者是Return
                {
                    block.End = index;
                }
                Blocks.Add(block);
            }
            RelocateJumpAddrToBlock();
        }
        private void RelocateJumpAddrToBlock()
        {
            //重定向节点的跳转地址为对基本块的引用
            //构造前驱后继
            Blocks.Sort((t1, t2) => { return t1.Start.CompareTo(t2.Start); });
            int JumpValue = Convert.ToInt32(QuadrupleType.JMP);
            Dictionary<int, Block> dict = new Dictionary<int, Block>();
            foreach (var i in Blocks)
            {
                dict.Add(i.Start, i);
            }
            for (int i = 0; i < Blocks.Count; ++i)
            {
                Block block = Blocks[i];
                QuadrupleNode node = CodeSeg[block.End];
                if (Convert.ToInt32(node.Type) <= JumpValue)
                {
                    node.JumpAddr = dict[(int)node.Result];
                    dict[(int)node.Result].JumpInsAddr.Add(node);
                    block.Next.Add(node.JumpAddr);
                    node.JumpAddr.Prev.Add(block);
                }
                if (IsConditionJump(block.End))
                {
                    if (i != Blocks.Count - 1)
                    {
                        if (!block.Next.Contains(Blocks[i + 1]))
                        {
                            block.Next.Add(Blocks[i + 1]);
                        }
                        if (!Blocks[i + 1].Prev.Contains(block))
                        {
                            Blocks[i + 1].Prev.Add(block);
                        }
                    }
                }
            }
            //Parallel.For(0, CodeSeg.Count, (i) =>
            foreach (var block in Blocks)
            {
                // var node = CodeSeg[i];
                for (int i = block.Start; i <= block.End; ++i)
                {
                    var node = CodeSeg[i];
                    if (node?.Type == QuadrupleType.Call)
                    {
                        node.JumpAddr = dict[(int)node.Result];

                        if (!block.Next.Contains(node.JumpAddr))
                        {
                            block.Next.Add(node.JumpAddr);
                        }
                        if (!node.JumpAddr.Next.Contains(block))//对call形成的前驱后继特殊处理
                        {
                            node.JumpAddr.Next.Add(block);
                        }
                        if (!node.JumpAddr.Prev.Contains(block))
                        {
                            node.JumpAddr.Prev.Add(block);
                        }
                    }
                }
            }
        }
        private void FindEntranceStatement(Queue<int> EntranceQueue)
        {
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < CodeSeg.Count; ++i)
            {
                visited[i] = false;
            }
            q.Enqueue(EntranceBlock == null ? CodeEntrance : EntranceBlock.Start); //第一次划分基本块使用传入变量，第二次开始使用重定位后的入口值
            while (q.Count != 0)
            {
                int index = q.Dequeue();
                if (index >= CodeSeg.Count || visited[index])
                {
                    continue;
                }
                visited[index] = true;//最开始为true表示为入口语句
                EntranceQueue.Enqueue(index);
                while (index < CodeSeg.Count && NotJumpOrReturn(index))
                {
                    if (CodeSeg[index]?.Type == QuadrupleType.Call)
                    {
                        var call = CodeSeg[index];
                        q.Enqueue(call.JumpAddr == null ? (int)call.Result : call.JumpAddr.Start);
                    }
                    ++index;
                }
                if (index >= CodeSeg.Count)
                {
                    continue;
                }
                if (IsConditionJump(index))
                {
                    q.Enqueue(index + 1);
                    q.Enqueue((int)CodeSeg[index].Result);
                }
                else if (CodeSeg[index].Type == QuadrupleType.JMP)
                {
                    q.Enqueue((int)CodeSeg[index].Result);
                }
            }
        }
        private bool IsConditionJump(int index)
        {
            if (index >= CodeSeg.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            QuadrupleNode node = CodeSeg[index];
            if (node == null)
            {
                return false;
            }
            if (Convert.ToInt32(node.Type) <= Convert.ToInt32(QuadrupleType.JNO))
            {
                return true;
            }
            return false;
        }
        private bool NotJumpOrReturn(int index)
        {
            if (index >= CodeSeg.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            QuadrupleNode node = CodeSeg[index];
            if (node == null)
            {
                return true;
            }
            if (Convert.ToInt32(node.Type) <= Convert.ToInt32(QuadrupleType.Return))
            {
                return false;
            }
            return true;
        }
        private List<QuadrupleNode> CodeSeg;
        private List<QuadrupleNode> VarSeg;
        private List<Block> Blocks;
        private int CodeEntrance;
        private bool[] visited;
        private int SerialNumber = 0;
        private bool[] DectectedInductionVar;
    }
}