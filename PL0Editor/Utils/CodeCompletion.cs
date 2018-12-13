using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Compiler;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace PL0Editor
{
    class CodeCompletion
    {
        internal List<Envirment> Symbols;
        internal List<Envirment> Temp;
        internal Envirment Global;
        internal MainWindow Window;
        Parser parser;

        public CodeCompletion(MainWindow host)
        {
            Envirment.Initial();
            Symbols = new List<Envirment>();
            Temp = new List<Envirment>();
            parser = new Parser();
            Global = new Envirment(null);
            Window = host;
        }
        public void Analyze(string Code)
        {
            try
            {
                Temp.Clear();
                Global.Clear();
                foreach (var i in Envirment.Keys)
                {
                    Global.Reserve(i, EType.Keyword);
                }
                UpdateSymbols(parser.Parse(Code), null);
                //Func<Envirment, Envirment, int> cmp = (i, j) =>
                //{
                //    if (i.Start.Equals(j.Start))
                //    {
                //        return i.End.CompareTo(j.End);
                //    }
                //    return i.Start.CompareTo(j.Start);
                //};
                ///Temp.Sort(new Comparison<Envirment>(cmp));

                if (Temp.Count == 0)
                {
                    Temp.Add(new Envirment(null));
                }
                foreach (var i in Envirment.Keys)
                {
                    Temp.Last().Reserve(i, EType.Keyword);
                }
                lock (Symbols)
                {
                    Symbols = Temp;
                }
            }
            catch (Exception) { }
        }
        private void UpdateSymbols(AstNode Root, Envirment prev)
        {
            try
            {
                if (Root == null)
                {
                    return;
                }
                Envirment env = new Envirment(prev)
                {
                    Start = Root.Left.Location
                };

                AstNode constDefine = Root.Left.Left.Left,
                    varDefine = Root.Left.Left.Right,
                    procDefine = Root.Left.Right,
                    stmt = Root.Right;
                List<AstNode> consts = constDefine.Info as List<AstNode>,
                    vars = varDefine.Info as List<AstNode>,
                    procs = procDefine.Info as List<AstNode>,
                    stmts = (List<AstNode>)stmt?.Info;
                if (consts != null)
                {
                    foreach (var i in consts)
                    {
                        env.Reserve((string)i.Left.Info, EType.Const);
                        Global.Reserve((string)i.Left.Info, EType.Const);
                    }
                    env.End = constDefine.Location;
                }
                if (vars != null)
                {
                    foreach (var i in vars)
                    {
                        env.Reserve((string)i.Left.Info, EType.Var);
                        Global.Reserve((string)i.Left.Info, EType.Var);
                    }
                    env.End = varDefine.Location;
                }
                if (procs != null)
                {
                    foreach (var i in procs)
                    {
                        env.Reserve((string)i.Left.Info, EType.Proc);
                        Global.Reserve((string)i.Left.Info, EType.Proc);
                        UpdateSymbols(i.Right, env);
                    }
                    env.End = procDefine.Location;
                }
                if (stmts != null)
                {
                    if (stmts.Count > 0)
                    {
                        if (stmt.Type == AstType.Statements)
                        {
                            try
                            {
                                env.End = stmts[stmts.Count - 1].Location;
                            }
                            catch
                            {

                            }
                        }
                    }
                }
                else
                {
                    if (stmt != null)
                    {
                        env.End = stmt.Location;
                    }
                }
                if (stmt != null)
                {
                    env.End = stmt.Location;
                    AstNode node = stmt;
                    bool Loop = true;
                    while (Loop)
                    {
                        switch (node.Type)
                        {
                            case AstType.WhileDo:
                            case AstType.RepeatUntil:
                                env.End = node.Location;
                                node = node.Right;
                                break;
                            case AstType.IfElse:
                                if (node.Right != null)
                                {
                                    env.End = node.Location;
                                    node = node.Right;
                                }
                                else
                                {
                                    env.End = node.Location;
                                    node = node.Left.Right;
                                }
                                break;
                            case AstType.Statements:
                                List<AstNode> list = node.Info as List<AstNode>;
                                if (list.Count > 0)
                                {
                                    node = list[list.Count - 1];
                                    env.End = node.Location;
                                }
                                else
                                {
                                    Loop = false;
                                }
                                break;
                            default:
                                if (node != null)
                                {
                                    env.End = node.Location;
                                }
                                Loop = false;
                                break;
                        }
                    }
                }
                Temp.Add(env);//避免排序
            }
            catch { }
        }
        internal class Envirment
        {
            protected Envirment prev;
            internal Position Start;
            internal Position End;
            protected HashSet<CompletionInfo> dict;
            public static HashSet<string> Keys;
            public void Clear()
            {
                dict.Clear();
            }
            public static void Initial()
            {
                Keys = new HashSet<string>(new string[] { "procedure", "if", "then", "else", "while", "do", "call", "begin", "repeat", "until", "read", "write", "var", "const", "end", "odd" });
            }
            public Envirment(Envirment prevEnv)
            {
                prev = prevEnv;
                dict = new HashSet<CompletionInfo>();
            }

            public void Reserve(string id, EType Type)
            {
                dict.Add(new CompletionInfo(id, Type));
            }

            public List<CompletionInfo> Find(char startChar)
            {
                List<CompletionInfo> Id = new List<CompletionInfo>();
                Envirment iter = this;
                while (iter != null)
                {
                    foreach (var i in iter.dict)
                    {
                        if (i.Info.Length > 0 && i.Info[0] == startChar)
                        {
                            Id.Add(i);
                        }
                    }
                    iter = iter.prev;
                }
                return Id;
            }
        }
    }
    public class CompletionInfo
    {
        public string Info;
        public EType Type;
        public CompletionInfo(string info, EType type)
        {
            Type = type;
            Info = info;
        }
    }
    public enum EType
    {
        Keyword,
        Var,
        Const,
        Proc
    }
    public class CompletionData : ICompletionData
    {
        public CompletionInfo Data;
        public delegate void SetStatusDelegate(string str);
        public SetStatusDelegate SetStatus;
        public CompletionData(CompletionInfo data)
        {
            this.Text = data.Info;
            Data = data;
        }

        public System.Windows.Media.ImageSource Image
        {
            get { return null; }
        }

        public string Text { get; private set; }

        // Use this property if you want to show a fancy UIElement in the drop down list.
        public object Content
        {
            get { return this.Text; }
        }

        public object Description
        {
            get { return Enum.GetName(typeof(EType), Data.Type); }
        }

        public double Priority { get { return 0; } }

        public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            try
            {
                textArea.Document.Replace(MainWindow.StartIndex, MainWindow.Length, this.Text);
            }
            catch
            {
                //SetStatus?.BeginInvoke("代码提示模块出现错误", null, null);
            }
        }

    }
}
