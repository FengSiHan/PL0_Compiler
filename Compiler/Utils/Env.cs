using System.Collections.Generic;

namespace Compiler
{
    /// <summary>
    /// 语法分析时的符号表
    /// </summary>
    internal class Env
    {
        internal Env prev;

        internal Dictionary<string, AstNode> dict;

        internal static HashSet<string> Keys;

        internal static void Initial()
        {
            Keys = new HashSet<string>(new string[] { "procedure", "if", "then", "else", "while", "do", "call", "begin", "repeat", "until", "read", "write", "var", "const", "end", "odd" });
        }

        internal Env(Env prevEnv)
        {
            prev = prevEnv;
            dict = new Dictionary<string, AstNode>();
        }

        /*
         * -1: keys
         *  0: ok
         *  1: unknown
         *  2: already exist
         */
        internal int Reserve(AstNode node)
        {
            if (node.Type == AstType.Var || node.Type == AstType.Const || node.Type == AstType.ProcDefine)
            {
                string name = (string)node.Left.Info;
                if (Keys.Contains(name))
                {
                    return -1;
                }
                if (dict.ContainsKey(name))
                {
                    return 2;
                }
                dict.Add((string)node.Left.Info, node);
                return 0;
            }
            else
            {
                return 1;
            }
        }

        internal AstNode Find(string ID)
        {
            for (var e = this; e != null; e = e.prev)
            {
                if (e.dict.ContainsKey(ID)) return e.dict[ID];
            }
            return null;
        }

        
        internal AstNode FindNoRecursion(string ID)
        {
            if (dict.ContainsKey(ID)) return dict[ID];
            return null;
        }
        
    }
}
