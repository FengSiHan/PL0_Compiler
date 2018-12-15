using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compiler;
using System.Collections;
using System.Windows;

namespace PL0Editor
{
    public partial class MainWindow
    {
        public void FormatCode(object sender, RoutedEventArgs e)
        {

            try
            {
                Temp.Clear();
                string SourceCode = CodeEditor.Text;
                Parser parser = new Parser();
                var root = parser.Parse(SourceCode);
                if (parser.GetNumofErrors() > 0)
                {
                    StatusContent.Text = "代码格式化之前请改正所有错误";
                    return;
                }
                GenerateCode(root, null, 0);
                Temp.Append("\n.");
                CodeEditor.Text = Temp.ToString();
            }
            catch (Exception) { }
        }
        private void TranslateExpr(AstNode Node)
        {
            switch (Node.Type)
            {
                case AstType.Var:
                case AstType.Const:
                    Temp.Append(Node.Left.Info);
                    break;
                case AstType.NUM:
                    Temp.Append(Node.Info);
                    break;
                default:
                    TranslateExpr(Node.Left);
                    Temp.Append($" {Node.Info} ");
                    TranslateExpr(Node.Right);
                    break;
            }
        }
        private void GenerateCode(AstNode Node, AstNode Prev, int Indent)
        {
            if (ReferenceEquals(Node, null))
            {
                return;
            }
            switch (Node.Type)
            {
                case AstType.SubProgram:
                    GenerateCode(Node.Left, Node, Indent);
                    GenerateCode(Node.Right, Node, Indent);
                    break;
                case AstType.Define:
                case AstType.IdDefine:
                    GenerateCode(Node.Left, Node, Indent);
                    GenerateCode(Node.Right, Node, Indent);
                    break;
                case AstType.ConstDefine:
                    List<AstNode> list = Node.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("const ");
                    for (int i = 0; i < list.Count - 1; ++i)
                    {
                        Temp.Append($"{list[i].Left.Info} = {list[i].Right.Info}, ");
                    }
                    Temp.Append($"{list[list.Count - 1].Left.Info} = {list[list.Count - 1].Right.Info};\n");
                    break;
                case AstType.VarDefine:
                    list = Node.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("var ");
                    for (int i = 0; i < list.Count - 1; ++i)
                    {
                        Temp.Append($"{list[i].Left.Info}, ");
                    }
                    Temp.Append($"{list[list.Count - 1].Left.Info};\n");
                    break;
                case AstType.ProcsDefine:
                    list = Node.Info as List<AstNode>;
                    if (list == null)
                    {
                        return;
                    }
                    foreach (var i in list)
                    {
                        for (int j = 0; j < Indent; ++j)
                        {
                            Temp.Append(IndentString);
                        }
                        Temp.Append($"procedure {i.Left.Info};\n");
                        GenerateCode(i.Right, Node, Indent + 1);
                        Temp.Append(";\n");
                    }
                    break;
                case AstType.Statements:
                    list = Node.Info as List<AstNode>;
                    if (Prev?.Type != AstType.RepeatUntil)
                    {
                        for (int i = 0; i < Indent; ++i)
                        {
                            Temp.Append(IndentString);
                        }
                        Temp.Append("begin\n");
                        Indent++;
                    }
                    if (list.Count != 0)
                    {
                        for (int i = 0; i < list.Count - 1; ++i)
                        {
                            GenerateCode(list[i], Node, Indent);
                            Temp.Append(";\n");
                        }
                        GenerateCode(list[list.Count - 1], Node, Indent);
                    }
                    if (Prev?.Type != AstType.RepeatUntil)
                    {
                        Temp.Append('\n');
                        for (int i = 0; i < Indent - 1; ++i)
                        {
                            Temp.Append(IndentString);
                        }
                        Temp.Append("end");
                    }
                    break;
                case AstType.Assign:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append($"{Node.Left.Left.Info} := ");
                    TranslateExpr(Node.Right);
                    break;
                case AstType.Call:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append($"call {Node.Info}");
                    break;
                case AstType.IfElse:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("if ");
                    if (Node.Left.Left.Info is string && (string)Node.Left.Left.Info == "odd")
                    {
                        Temp.Append("odd ");
                        TranslateExpr(Node.Left.Left.Left);
                    }
                    else
                    {
                        TranslateExpr(Node.Left.Left.Left);
                        Temp.Append($" {Node.Left.Left.Info} ");
                        TranslateExpr(Node.Left.Left.Right);
                    }
                    Temp.Append(" then\n");
                    GenerateCode(Node.Left.Right, Node, Indent + 1);
                    Temp.Append('\n');
                    if (Node.Right != null)
                    {
                        for (int i = 0; i < Indent; ++i)
                        {
                            Temp.Append(IndentString);
                        }
                        Temp.Append("else\n");
                        GenerateCode(Node.Right, Node, Indent + 1);
                    }
                    break;
                case AstType.RepeatUntil:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("repeat\n");
                    GenerateCode(Node.Right, Node, Indent + 1);
                    Temp.Append('\n');
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("until ");
                    if (Node.Left.Info is string && (string)Node.Left.Info == "odd")
                    {
                        Temp.Append("odd ");
                        TranslateExpr(Node.Left.Left);
                    }
                    else
                    {
                        TranslateExpr(Node.Left.Left);
                        Temp.Append($" {Node.Left.Info} ");
                        TranslateExpr(Node.Left.Right);
                    }
                    break;
                case AstType.WhileDo:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("while ");
                    if (Node.Left.Info is string && (string)Node.Left.Info == "odd")
                    {
                        Temp.Append("odd ");
                        TranslateExpr(Node.Left.Left);
                    }
                    else
                    {
                        TranslateExpr(Node.Left.Left);
                        Temp.Append($" {Node.Left.Info} ");
                        TranslateExpr(Node.Left.Right);
                    }
                    Temp.Append('\n');
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("do\n");
                    GenerateCode(Node.Right, Node, Indent + 1);
                    break;
                case AstType.Read:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("read(");
                    List<AstNode> param = Node.Info as List<AstNode>;
                    for (int i = 0; i < param.Count - 1; ++i)
                    {
                        var t = param[i];
                        Temp.Append($"{t.Left.Info}, ");
                    }
                    Temp.Append($"{param[param.Count - 1].Left.Info})");
                    break;
                case AstType.Write:
                    for (int i = 0; i < Indent; ++i)
                    {
                        Temp.Append(IndentString);
                    }
                    Temp.Append("write(");
                    param = Node.Info as List<AstNode>;
                    for (int i = 0; i < param.Count - 1; ++i)
                    {
                        var t = param[i] as AstNode;
                        Temp.Append($"{t.Left.Info}, ");
                    }
                    Temp.Append($"{param[param.Count - 1].Left.Info})");
                    break;
            }
        }
        private StringBuilder Temp;
        private string IndentString = "    ";
    }
}
