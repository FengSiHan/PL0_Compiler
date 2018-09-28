using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class Parser
    {
        /*                                        |-->ConstDefine   ---
         *                          |-->IdDefine--|                   |->
         *             |-->Define--->             |-->VarDefine     ---
         *Subprocedure |            |-->ProcDefine
         *             |-->Statement
         */


        #region ---Funciton---
        public Parser()
        {
            Keys = new HashSet<string>(new string[] { "procedure", "if", "while", "call", "begin", "repeat", "read", "write", "var", "const", "end" });
            SkipControlList = new List<Token>();
            Env.Initial();
        }

        public AstNode Parse(string Text, bool WritingCode = false)
        {
            var lexer = new Lexer(Text);
            tokens = lexer.Scan().GetEnumerator();
            ErrorMsg = lexer.ErrorMsg;
            if (!tokens.MoveNext())
            {
                return null;
            }
            SkipControlList.Clear();
            try
            {
                SkipControlList.Add(Token.PERIOD);
                AstTree = SubProgram();
                CheckExpectToken(Token.PERIOD);
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
            }
            catch (Exception e)
            {
                if (ErrorMsg.Errors.Count != 0)
                {
                    ErrorMsg.Add("Expect more code", CurrentToken()?.Location);
                }
            }
            finally
            {
                SkipControlList.RemoveAt(SkipControlList.Count - 1);
            }
            if (!WritingCode)
            {
                StaticCodeAnalysis(null, AstTree);
            }
            ErrorMsg.SortErrorMsgByLine();
            return AstTree;
        }

        public void StaticCodeAnalysis(Env preenv, AstNode subproc) //递归分程序解析
        {
            if (subproc == null)
                return;

            Env env = new Env(preenv);
            SubProgram();
            AstNode constDefine = subproc.Left.Left.Left,
                varDefine = subproc.Left.Left.Right,
                procDefine = subproc.Left.Right,
                stmt = subproc.Right;
            List<AstNode> consts = constDefine.Info as List<AstNode>,
                vars = varDefine.Info as List<AstNode>,
                procs = procDefine.Info as List<AstNode>;

            /*
             * -1: keys
             *  0: ok
             *  1: unknown
             *  2: already exist
             */
            if (consts != null)
            {
                foreach (var i in consts)
                {
                    int res = env.Reserve(i);
                    switch (res)
                    {
                        case -1:
                            ErrorMsg.Add($"'{i.Left.Info}' is reserved,it can't be id", i.Location);
                            break;
                        case 1:
                            ErrorMsg.Add($"Unknown Token '{i.Left.Info}'", i.Location);
                            break;
                        case 2:
                            ErrorMsg.Add($"Unexpected Token '{i.Left.Info}', any identifier can only be declared once", i.Location);
                            break;
                    }
                }
            }
            if (vars != null)
            {
                foreach (var i in vars)
                {
                    int res = env.Reserve(i);
                    switch (res)
                    {
                        case -1:
                            ErrorMsg.Add($"'{i.Left.Info}' is reserved,it can't be id", i.Location);
                            break;
                        case 1:
                            ErrorMsg.Add($"Unknown Token '{i.Left.Info}'", i.Location);
                            break;
                        case 2:
                            ErrorMsg.Add($"Unexpected Token '{i.Left.Info}', any identifier can only be declared once", i.Location);
                            break;
                    }
                }
            }
            //递归解析
            if (procs != null)
            {
                foreach (var i in procs)
                {
                    int res = env.Reserve(i);
                    switch (res)
                    {
                        case -1:
                            ErrorMsg.Add($"'{i.Left.Info}' is reserved,it can't be id", i.Location);
                            break;
                        case 1:
                            ErrorMsg.Add($"Unknown Token '{i.Left.Info}'", i.Location);
                            break;
                        case 2:
                            ErrorMsg.Add($"Unexpected Token '{i.Left.Info}', any identifier can only be declared once", i.Location);
                            break;
                        case 0:
                            StaticCodeAnalysis(env, i.Right);
                            break;
                    }
                }
            }
            if (stmt != null)
            {
                if (stmt.Type == ExprType.Statements)
                {
                    foreach (var i in (List<AstNode>)stmt.Info)
                    {
                        VerifyIdentifier(env, i);
                    }
                }
                else
                {
                    VerifyIdentifier(env, stmt);
                }
            }
        }

        public void PrintErrorMsg()
        {
            ErrorMsg.PrintErrorMsg();
        }
        public int GetNumofErrors()
        {
            return ErrorMsg.Count();
        }
        #endregion

        #region --- Syntax Analysis---
        //所有函数满足任意一次 匹配/错误部分跳过 完成后，current都指向下一个待解析的Token,匹配失败仍然满足current指向待解析Token
        private AstNode SubProgram()
        {
            AstNode left = new AstNode(ExprType.Define,
                new AstNode(ExprType.IdDefine, left: new AstNode(ExprType.ConstDefine), right: new AstNode(ExprType.VarDefine)),
                new AstNode(ExprType.ProcsDefine),
                location: CurrentToken()?.Location)
            , right = new AstNode()
            {
                Type = ExprType.Statement
            };

            Determine(CurrentToken(), t => { return t == Token.CONST; }, ref left.Left.Left, DefineConst);
            Determine(CurrentToken(), t => { return t == Token.VAR; }, ref left.Left.Right, DefineVar);
            Determine(CurrentToken(), t => { return t == Token.PROC; }, ref left.Right, DefineProcedure);
            Determine(CurrentToken(), t => { return true; }, ref right, Statement);
            return new AstNode(ExprType.SubProgram, left, right, location: left.Location);//分程序
        }

        private AstNode DefineConst()
        {
            List<AstNode> Definition = new List<AstNode>();
            Position position = CurrentToken()?.Location;
            if (CurrentToken() == null)
            {
                return new AstNode(ExprType.ConstDefine, null, null, Definition, position);
            }
            SkipControlList.Add(Token.SEMICOLON);
            try
            {
                if (!tokens.MoveNext() || CurrentToken().TokenType != TokenType.ID)
                {
                    throw new SyntaxErrorException("Missing const definition", position);
                }
                while (CurrentToken()?.TokenType != TokenType.SEMICOLON)
                {
                    Token peek = CurrentToken();
                    if (peek.TokenType != TokenType.ID)
                    {
                        throw new SyntaxErrorException($"Unexpected Token '{peek.Content}',Expect ID in const declaration", peek.Location);
                    }

                    AstNode node = new AstNode(ExprType.Const,
                        new AstNode(ExprType.ConstID, null, null, peek.Content, peek.Location),
                        location: peek.Location)
                    {
                        Initialized = true
                    };
                    //Left is Node of ID,Right is its value
                    if (!tokens.MoveNext())
                    {
                        break;
                    }
                    Token next = CurrentToken();
                    if (next == null || next.TokenType == TokenType.SEMICOLON)
                    {
                        Definition.Add(node);
                        break;
                    }

                    if (next.TokenType == TokenType.COMMA)
                    {
                        Definition.Add(node);
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                    }
                    else if (next.TokenType == TokenType.OP && next.Content is Char && (char)next.Content == '=')
                    {
                        if (!tokens.MoveNext())
                        {
                            throw new SyntaxErrorException("Expect value for assign", next.Location);
                        }

                        Definition.Add(node);
                        if (CurrentToken().TokenType != TokenType.NUM)
                        {
                            ErrorMsg.Add($"Unexpected Token '{CurrentToken().Content}', Only number can be assigned to const ID", next.Location);

                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                            while (CurrentToken()?.TokenType != TokenType.COMMA
                                && CurrentToken().TokenType != TokenType.SEMICOLON
                                && CurrentToken().TokenType != TokenType.PERIOD
                                && Keys.Contains(CurrentToken().Content) == false)
                            {
                                if (!tokens.MoveNext())
                                {
                                    break;
                                }
                            }
                            if (CurrentToken() == null || CurrentToken().TokenType != TokenType.COMMA)
                            {
                                break;
                            }
                        }
                        else
                        {
                            node.Right = new AstNode(ExprType.NUM, null, null, CurrentToken().Content, CurrentToken().Location);
                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                            if (CurrentToken() == null || CurrentToken().TokenType != TokenType.COMMA)
                            {
                                break;
                            }
                        }
                        if (!tokens.MoveNext())//正确情况下指向了 , ;
                        {
                            break;
                        } 

                    }

                    else if (next.TokenType == TokenType.ASSIGN)
                    {
                        ErrorMsg.Add("In const declaration, := should be =", next.Location);
                        if (!tokens.MoveNext()) throw new SyntaxErrorException("Expect value for assignment", next.Location);
                        if (CurrentToken().TokenType != TokenType.NUM)
                        {
                            ErrorMsg.Add($"Unexpected Token '{next.Content}',Only number can be assigned to const ID", next.Location);
                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                            while (CurrentToken()?.TokenType != TokenType.COMMA ||
                                CurrentToken().TokenType != TokenType.SEMICOLON && CurrentToken().TokenType != TokenType.PERIOD
                                && Keys.Contains(CurrentToken().Content) == false)
                            {
                                if (!tokens.MoveNext())
                                {
                                    break;
                                }
                            }
                            if (CurrentToken() == null && CurrentToken().TokenType != TokenType.COMMA)
                            {
                                break;
                            }
                        }
                        else
                        {
                            node.Right = new AstNode(ExprType.NUM, null, null, CurrentToken().Content, CurrentToken().Location);
                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                            Definition.Add(node);
                            if (CurrentToken() == null || CurrentToken().TokenType != TokenType.COMMA)
                            {

                                break;
                            }
                        }
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                    }
                    else if (CurrentToken() != null && CurrentToken().Content is string && Keys.Contains(CurrentToken().Content))
                    {
                        throw new SyntaxErrorException("Missing Expected Token ';' in const declaration", position);
                    }
                    else if (CurrentToken().TokenType == TokenType.ID)
                    {
                        throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}',there should be a ',' or ';' in front of it", CurrentToken().Location);
                    }
                    else throw new SyntaxErrorException($"Unexpected Token '{next.Content}'", next.Location);
                }
                CheckExpectToken(Token.SEMICOLON);
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
                SkipErrorTokens();
                if (CurrentToken() == Token.SEMICOLON)
                {
                    tokens.MoveNext();
                }
            }
            finally
            {
                SkipControlList.RemoveAt(SkipControlList.Count - 1);
            }
            return new AstNode(ExprType.ConstDefine, null, null, Definition, position);
        }

        private AstNode DefineVar()
        {
            List<AstNode> Definition = new List<AstNode>();
            Position position = CurrentToken()?.Location;
            SkipControlList.Add(Token.SEMICOLON);
            if (CurrentToken() == null)
            {
                return new AstNode(ExprType.VarDefine, null, null, Definition, position);
            }
            try
            {
                if (!tokens.MoveNext() || CurrentToken().TokenType != TokenType.ID)
                {
                    throw new SyntaxErrorException("Missing Var Definition", position);
                }
                while (CurrentToken()?.TokenType != TokenType.SEMICOLON)
                {
                    Token peek = CurrentToken();
                    if (peek.TokenType != TokenType.ID)
                    {
                        throw new SyntaxErrorException($"Unexpected Token '{peek.Content}',Expect ID in var declaration", peek.Location);
                    }

                    AstNode node = new AstNode(ExprType.Var, new AstNode(ExprType.VarID, null, null, peek.Content, peek.Location), location: peek.Location); //Left is Node of ID,Right is its value

                    if (!tokens.MoveNext())
                    {
                        break;
                    }
                    Token next = CurrentToken();
                    if (next == null || next.TokenType == TokenType.SEMICOLON)
                    {
                        Definition.Add(node);
                        break;
                    }

                    if (next.TokenType == TokenType.COMMA)
                    {
                        Definition.Add(node);
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                    }
                    else if (next.TokenType == TokenType.OP && next.Content is Char && (char)next.Content == '=')
                    {
                        if (tokens.MoveNext())
                        {
                            ErrorMsg.Add("var ID can't be assigned when declared", next.Location);
                        }
                        else
                        {
                            break;
                        }
                        Definition.Add(node);
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                        while (CurrentToken()?.TokenType != TokenType.COMMA
                            && CurrentToken().TokenType != TokenType.SEMICOLON
                            && CurrentToken().TokenType != TokenType.PERIOD
                            && Keys.Contains(CurrentToken().Content) == false)
                        {
                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                        }
                        if (CurrentToken() == null || CurrentToken().TokenType != TokenType.COMMA)
                        {
                            break;
                        }
                        if (!tokens.MoveNext())
                        {
                            break;
                        } // 指向下一个ID
                    }
                    else if (next.TokenType == TokenType.ASSIGN)
                    {
                        if (tokens.MoveNext())
                        {
                            ErrorMsg.Add($"var ID can't be assigned when declared at Line", next.Location);
                        }
                        Definition.Add(node);
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                        while (CurrentToken()?.TokenType != TokenType.COMMA
                            && CurrentToken().TokenType != TokenType.SEMICOLON
                            && CurrentToken().TokenType != TokenType.PERIOD
                            && Keys.Contains(CurrentToken().Content) == false)
                        {
                            if (!tokens.MoveNext())
                            {
                                break;
                            }
                        }
                        if (CurrentToken() == null || CurrentToken().TokenType != TokenType.COMMA)
                        {
                            break;
                        }
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                    }
                    else if (CurrentToken() != null && CurrentToken().Content is string && Keys.Contains(CurrentToken().Content))
                    {
                        throw new SyntaxErrorException("Missing Expected Token ';' in var declaration", position);
                    }
                    else if (CurrentToken().TokenType == TokenType.ID)
                    {
                        throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}',there should be a ',' or ';' in front of it", position);
                    }
                    else throw new SyntaxErrorException($"Unexpected Token '{next.Content}'", next.Location);
                }
                CheckExpectToken(Token.SEMICOLON);
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
                SkipErrorTokens();
                if (CurrentToken() == Token.SEMICOLON)
                {
                    tokens.MoveNext();
                }
            }
            finally
            {
                SkipControlList.RemoveAt(SkipControlList.Count - 1);
            }
            return new AstNode(ExprType.VarDefine, null, null, Definition, position);
        }

        private AstNode DefineProcedure() // 可能为数个procedure声明
        {
            List<AstNode> Definition = new List<AstNode>();
            Position position = CurrentToken().Location;
            if (CurrentToken() == null)
            {
                return new AstNode(ExprType.ProcsDefine, null, null, Definition, position);
            }
            while (CurrentToken() == Token.PROC)
            {
                try
                {
                    if (!tokens.MoveNext() || CurrentToken().TokenType != TokenType.ID)
                    {
                        throw new SyntaxErrorException("Missing Procedure Definition ", position);
                    }

                    AstNode left = new AstNode(ExprType.ProcDefine, null, null, CurrentToken().Content, CurrentToken().Location);
                    if (!tokens.MoveNext())
                    {
                        break;
                    }
                    CheckExpectToken(Token.SEMICOLON);

                    AstNode right = SubProgram();
                    AstNode proc = new AstNode(ExprType.ProcDefine, left, right, location: left.Location);
                    Definition.Add(proc);
                    CheckExpectToken(Token.SEMICOLON);
                }
                catch (SyntaxErrorException e)
                {
                    ErrorMsg.Add(e);
                    if (CurrentToken()?.TokenType == TokenType.SEMICOLON)
                    {
                        Position Line1 = CurrentToken().Location;
                        if (!tokens.MoveNext())
                        {
                            break;
                        }
                        if (CurrentToken() == Token.PROC)
                        {
                            continue;
                        }
                        ErrorMsg.Add("Surplus ;", Line1);
                    }
                }
            }
            return new AstNode(ExprType.ProcsDefine, null, null, Definition, position);
        }

        private AstNode Statement()
        {
            AstNode node = null;
            if ((CurrentToken()?.TokenType == TokenType.ID) == false)
            {
                return null;
            }
            try
            {
                switch (CurrentToken().Content)
                {
                    case "if":
                        SkipControlList.Add(Token.SEMICOLON);

                        AstNode node_ifelse = new AstNode(ExprType.IfElse, CurrentToken().Location);
                        AstNode node_if = new AstNode(ExprType.IfElse, CurrentToken().Location);

                        CheckExpectToken(Token.IF);

                        SkipControlList.Add(Token.THEN); //avoid skipping 'then'
                        node_if.Left = ConditionExpr();
                        SkipControlList.RemoveAt(SkipControlList.Count - 1);

                        CheckExpectToken(Token.THEN);
                        node_if.Right = Statement();

                        node_ifelse.Left = node_if;

                        Position line = CurrentToken().Location;
                        if (CurrentToken().Content is string && (string)CurrentToken().Content == "else")
                        {
                            if (tokens.MoveNext())
                            {
                                node_ifelse.Right = Statement();
                            }
                            else
                            {
                                throw new SyntaxErrorException($"Expect Statement after 'else'", line);
                            }
                        }
                        return node_ifelse;
                    case "while":
                        SkipControlList.Add(Token.DO);

                        AstNode node_whiledo = new AstNode(ExprType.WhileDo, CurrentToken().Location);
                        CheckExpectToken(Token.WHILE);

                        SkipControlList.Add(Token.DO);//avoid skipping 'do'
                        node_whiledo.Left = ConditionExpr();
                        SkipControlList.RemoveAt(SkipControlList.Count - 1);

                        CheckExpectToken(Token.DO);
                        node_whiledo.Right = Statement();
                        return node_whiledo;
                    case "call":
                        SkipControlList.Add(Token.SEMICOLON);

                        CheckExpectToken(Token.CALL);
                        AstNode node_call = new AstNode(ExprType.Call, CurrentToken().Location);
                        Position Line = CurrentToken().Location;
                        if (CurrentToken()?.TokenType == TokenType.ID)
                        {
                            node_call.Info = CurrentToken().Content;
                            tokens.MoveNext();
                        }
                        else
                        {
                            throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}',Expect Function name after call", Line);
                        }
                        return node_call;
                    case "read":
                        SkipControlList.Add(Token.SEMICOLON);
                        Line = CurrentToken().Location;
                        CheckExpectToken(Token.READ);
                        CheckExpectToken(Token.LBRACKET);

                        if (CurrentToken() == Token.RBRACKET)
                        {
                            CheckExpectToken(Token.RBRACKET);
                            throw new SyntaxErrorException("Read funtion needs one or more arguments", Line);
                        }
                        node = new AstNode(ExprType.Read, Line);
                        List<AstNode> args = new List<AstNode>();
                        node.Info = args;
                        do
                        {
                            if (CurrentToken() == null)
                            {
                                throw new SyntaxErrorException("Expect ID for Read argument", Line);
                            }
                            else if (CurrentToken().TokenType != TokenType.ID)
                            {
                                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' can't be argument of Read function", Line);
                            }
                            args.Add(new AstNode(ExprType.UnknownID, info: CurrentToken().Content));
                            if (tokens.MoveNext() == false)
                            {
                                break;
                            }
                            else if (CurrentToken().TokenType == TokenType.BRACKET)
                            {
                                if ((char)CurrentToken().Content == '(')
                                {
                                    throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}'", Line);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' follow read", CurrentToken().Location);
                            }
                        }
                        while (tokens.MoveNext() && CurrentToken().TokenType != TokenType.BRACKET && Keys.Contains(CurrentToken().Content) == false);
                        CheckExpectToken(Token.RBRACKET);
                        return node;
                    case "write":
                        SkipControlList.Add(Token.SEMICOLON);
                        Line = CurrentToken().Location;
                        CheckExpectToken(Token.WRITE);
                        CheckExpectToken(Token.LBRACKET);

                        if (CurrentToken() == Token.RBRACKET)
                        {
                            CheckExpectToken(Token.RBRACKET);
                            throw new SyntaxErrorException("Write funtion needs one or more arguments", Line);
                        }

                        node = new AstNode(ExprType.Write, Line);
                        args = new List<AstNode>();
                        node.Info = args;
                        do
                        {
                            if (CurrentToken() == null)
                            {
                                throw new SyntaxErrorException("Expect ID for Write argument", Line);
                            }
                            else if (CurrentToken().TokenType != TokenType.ID)
                            {
                                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' can't be argument of write function", Line);
                            }
                            args.Add(new AstNode(ExprType.UnknownID, info: CurrentToken().Content));

                            if (tokens.MoveNext() == false)
                            {
                                break;
                            }
                            else if (CurrentToken().TokenType == TokenType.BRACKET)
                            {
                                if ((char)CurrentToken().Content == '(')
                                {
                                    throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}'", Line);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else if (CurrentToken().TokenType == TokenType.SEMICOLON)
                            {
                                throw new SyntaxErrorException("Missing ')' at the end of write function", Line);
                            }
                            else if (CurrentToken().TokenType != TokenType.COMMA)
                            {
                                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' follow write at Line", CurrentToken().Location);
                            }
                        }
                        while (tokens.MoveNext() && CurrentToken().TokenType != TokenType.BRACKET && Keys.Contains(CurrentToken().Content) == false);

                        CheckExpectToken(Token.RBRACKET);
                        return node;
                    case "begin":
                        SkipControlList.Add(Token.END);
                        Line = CurrentToken().Location;
                        CheckExpectToken(Token.BEGIN);
                        AstNode node_begin = Statements();
                        CheckExpectToken(Token.END);
                        return node_begin;
                    case "repeat":
                        SkipControlList.Add(Token.UNTIL);
                        Line = CurrentToken().Location;
                        CheckExpectToken(Token.REPEAT);
                        AstNode node_repeat = new AstNode(ExprType.RepeatUntil, Line, right: Statements());
                        CheckExpectToken(Token.UNTIL);
                        node_repeat.Left = ConditionExpr();
                        return node_repeat;
                    default:
                        //赋值语句
                        SkipControlList.Add(Token.SEMICOLON);
                        if (CurrentToken() == Token.UNTIL || CurrentToken() == Token.THEN
                            || CurrentToken() == Token.PROC || CurrentToken() == Token.ELSE
                            || CurrentToken() == Token.END || CurrentToken() == Token.CONST
                            || CurrentToken() == Token.VAR || CurrentToken() == Token.DO)
                        {
                            throw new SyntaxErrorException($"Unexcepted Token '{CurrentToken().Content}' ,Statement can't start with '{CurrentToken().Content}', Maybe there is a surplus ';' at the end of last statement", CurrentToken().Location);
                        }
                        AstNode node_assign = new AstNode(ExprType.Assign, CurrentToken().Location)
                        {
                            Left = new AstNode(ExprType.UnknownID, null, null, CurrentToken().Content, CurrentToken().Location)
                        };
                        tokens.MoveNext();
                        CheckExpectToken(Token.ASSIGN);
                        node_assign.Right = Expr();
                        return node_assign;
                }
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
                SkipErrorTokens();
                /* if (CurrentToken()?.TokenType == TokenType.SEMICOLON)  //是否可行 删除end前的;
                     tokens.MoveNext();*/
            }
            finally
            {
                SkipControlList.RemoveAt(SkipControlList.Count - 1);
            }
            return node; // node may be null
        }

        private AstNode Statements()
        {
            AstNode node = new AstNode(ExprType.Statements, CurrentToken()?.Location);
            if (CurrentToken() == null)
            {
                return node;
            }
            SkipControlList.Add(Token.SEMICOLON);
            try
            {
                var stmts_list = new List<AstNode>();
                node.Info = stmts_list;
                AstNode stmt = Statement();
                if (stmt != null)
                {
                    stmts_list.Add(stmt);
                }
                while (CurrentToken() == Token.SEMICOLON)
                {
                    if (!tokens.MoveNext())
                    {
                        break;
                    }
                    Position Line = CurrentToken().Location;
                    stmt = Statement();

                    if (stmt != null)
                    {
                        stmts_list.Add(stmt);
                    }
                }
            }
            catch (SyntaxErrorException e)
            {
                throw e;
            }
            finally
            {
                SkipControlList.RemoveAt(SkipControlList.Count - 1);
            }
            return node;
        }

        private AstNode Expr()
        {
            if (CurrentToken() == null)
            {
                return null;
            }
            AstNode node = new AstNode(ExprType.Expr, CurrentToken().Location);
            if (CurrentToken().TokenType == TokenType.OP && CurrentToken().Content is char)
            {
                if ((char)CurrentToken().Content == '-')
                {
                    AstNode l_node = new AstNode(ExprType.Minus, CurrentToken().Location)
                    {
                        Left = new AstNode(ExprType.NUM, null, null, 0, CurrentToken().Location),
                        Info = CurrentToken().Content
                    };
                    tokens.MoveNext();
                    l_node.Right = Term();
                    node.Left = l_node;
                }
                else if ((char)CurrentToken().Content == '+')  // + NUM 可以忽略
                {
                    tokens.MoveNext();
                }
                else
                {
                    throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}', Only + and - can be unary operator", CurrentToken().Location);
                }
            }
            if (node.Left == null)
            {
                node.Left = Term();
            }
            if (CurrentToken().TokenType == TokenType.OP && CurrentToken().Content is char
                && ((char)CurrentToken().Content == '-' || (char)CurrentToken().Content == '+'))
            {
                node.Info = CurrentToken().Content;
                tokens.MoveNext();
                node.Right = Expr();
            }
            else
            {
                return node.Left;
            }
            return node;
        }

        private AstNode Term()
        {
            AstNode node = Factor();
            if (CurrentToken()?.TokenType == TokenType.OP && CurrentToken().Content is char
                && ((char)CurrentToken().Content == '*' || (char)CurrentToken().Content == '/'))
            {
                node = new AstNode(ExprType.Term, node, info: CurrentToken().Content, location: CurrentToken().Location);
                tokens.MoveNext();
                node.Right = Term();
            }
            return node;
        }

        private AstNode Factor()
        {
            if (CurrentToken() == null)
            {
                throw new SyntaxErrorException("Missing Factor at End", new Position(0, 0));
            }
            if (CurrentToken().TokenType == TokenType.ID)
            {
                AstNode node = new AstNode(ExprType.UnknownID, null, null, CurrentToken().Content, CurrentToken().Location);
                tokens.MoveNext();
                return node;
            }
            else if (CurrentToken().TokenType == TokenType.NUM)
            {
                AstNode node = new AstNode(ExprType.NUM, null, null, CurrentToken().Content, CurrentToken().Location);
                tokens.MoveNext();
                return node;
            }
            else if (CurrentToken().TokenType == TokenType.BRACKET)
            {
                CheckExpectToken(Token.LBRACKET);
                AstNode node = Expr();
                CheckExpectToken(Token.RBRACKET);
                return node;
            }
            else
            {
                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}'", CurrentToken().Location);
            }
        }

        private AstNode ConditionExpr()
        {
            if (CurrentToken() == null)
            {
                return null;
            }
            AstNode node = new AstNode(ExprType.Condition, CurrentToken().Location);
            try
            {

                if (CurrentToken().Content is string && (string)CurrentToken().Content == "odd")
                {
                    node.Info = CurrentToken().Content;
                    tokens.MoveNext();
                    node.Left = Expr();
                }
                else
                {
                    Position Line = CurrentToken().Location;
                    node.Left = Expr();
                    if (CurrentToken() == null)
                    {
                        throw new SyntaxErrorException($"Expect Relation Operator", Line);
                    }
                    else
                    {
                        if (CurrentToken().Content is string)
                        {
                            switch ((string)CurrentToken().Content)
                            {
                                case "<=":
                                case ">=":
                                case "<>":
                                    node.Info = CurrentToken().Content;
                                    tokens.MoveNext();
                                    break;
                                default:
                                    throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' ,Expect Relation Operator", Line);
                            }
                        }
                        else if (CurrentToken().Content is char)
                        {
                            switch ((char)CurrentToken().Content)
                            {
                                case '<':
                                case '>':
                                case '=':
                                    node.Info = CurrentToken().Content;
                                    tokens.MoveNext();
                                    break;
                                default:
                                    throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' ,Expect Relation Operator", Line);
                            }
                        }
                        else
                            throw new SyntaxErrorException($"Unexpected Token '{CurrentToken().Content}' ,Expect Relation Operator", Line);
                    }
                    node.Right = Expr();
                }
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
                SkipErrorTokens();
            }
            if (node.Left == null && node.Right == null) return null;
            return node;
        }
        #endregion

        #region ---Utils and Member---
        private void VerifyIdentifier(Env env, AstNode stmt)//基本检查，并且把节点指向最开始声明的节点
        {
            if (stmt == null)
            {
                return;
            }
            switch (stmt.Type)
            {
                case ExprType.Assign:
                    AstNode id = env.Find((string)stmt.Left.Info);
                    if (id == null)
                    {
                        ErrorMsg.Add($"Unknown Token '{stmt.Left.Info}',it needs declaring", stmt.Location);
                    }
                    else if (id.Type == ExprType.Const || id.Type == ExprType.ProcDefine)
                    {
                        ErrorMsg.Add($"'{id.Left.Info}' can't be assigned,it's not variable", id.Location);
                    }
                    else if (id.Type != ExprType.Var)
                    {
                        ErrorMsg.Add($"'{id.Info}' can't be assigned,it's not variable", id.Location);
                    }
                    else
                    {
                        id.Initialized = true;
                        stmt.Left = id;
                    }
                    TraversalExpr(env, stmt.Right, stmt, false);
                    break;
                case ExprType.Call:
                    //注意只能call同级分程序
                    id = env.FindNoRecursion((string)stmt.Info);
                    if (id == null)
                    {
                        ErrorMsg.Add($"Unknown Token '{stmt.Info}',it needs declaring", stmt.Location);
                    }
                    else if (id.Type != ExprType.ProcDefine)
                    {
                        ErrorMsg.Add($"'{id.Info}' can't be called,it's not procedure", stmt.Location);
                    }
                    else
                    {
                        stmt.Left = id;
                    }
                    break;
                case ExprType.IfElse:
                    AstNode _if = stmt.Left, _else = stmt.Right;
                    TraversalExpr(env, _if.Left, _if, true);
                    VerifyIdentifier(env, _if.Right);
                    VerifyIdentifier(env, _else);
                    break;
                case ExprType.Read:
                    List<AstNode> tokenlist = (List<AstNode>)stmt.Info;
                    for (int i = 0; i < tokenlist.Count; ++i)
                    {
                        id = env.Find((string)tokenlist[i].Info);
                        if (id == null)
                        {
                            ErrorMsg.Add($"Unknown Token '{tokenlist[i].Info}',it needs declaring", stmt.Location);
                        }
                        else if (id.Type != ExprType.Var)
                        {
                            ErrorMsg.Add($"'{id.Left.Info}' can't be assigned,it's not variable", stmt.Location);
                        }
                        else
                        {
                            tokenlist[i] = id;
                            id.Initialized = true;
                        }
                    }
                    break;
                case ExprType.Write:
                    tokenlist = (List<AstNode>)stmt.Info;
                    for (int i = 0; i < tokenlist.Count; ++i)
                    {
                        id = env.Find((string)tokenlist[i].Info);
                        if (id == null)
                        {
                            ErrorMsg.Add($"Unknown Token '{tokenlist[i].Info}',it needs declaring", stmt.Location);
                        }
                        else if (id.Type != ExprType.Var && id.Type != ExprType.Const)
                        {
                            ErrorMsg.Add($"'{id.Left.Info}' is illegal,Write() requires const or var id", stmt.Location);
                        }
                        else
                        {
                            tokenlist[i] = id;
                        }
                    }
                    break;
                case ExprType.RepeatUntil:
                    VerifyIdentifier(env, stmt.Right);
                    if (stmt.Left == null)
                    {
                        ErrorMsg.Add("RepeatUntil condition has error", stmt.Location);
                    }
                    else
                    {
                        TraversalExpr(env, stmt.Left, stmt, true);
                    }
                    break;
                case ExprType.WhileDo:
                    VerifyIdentifier(env, stmt.Right);
                    if (stmt.Left == null)
                    {
                        ErrorMsg.Add("While condition has error", stmt.Location);
                    }
                    else
                    {
                        TraversalExpr(env, stmt.Left, stmt, true);
                    }
                    break;
                case ExprType.Statements:
                    var list = (List<AstNode>)stmt.Info;
                    foreach (var i in list)
                    {
                        VerifyIdentifier(env, i);
                    }
                    break;
            }
        }

        private void TraversalExpr(Env env, AstNode start, AstNode prev, bool ifLeft)
        {
            Queue<AstNode> q = new Queue<AstNode>();
            if (start.Type == ExprType.UnknownID || start.Type == ExprType.Var)
            {
                var node = env.Find((string)start.Info);
                var type = node?.Type;
                if (type == null)
                {
                    ErrorMsg.Add($"Unknown Token '{start.Info}',it needs declaring", start.Location);
                }
                else if (ExprType.Var == type)
                {
                    if (ifLeft)
                    {
                        prev.Left = node;
                    }
                    else
                    {
                        prev.Right = node;
                    }
                }
                else if (ExprType.ProcDefine == type)
                {
                    ErrorMsg.Add($"Procedure name '{node.Info}' can't be part of expression", start.Location);
                }
                else if (ExprType.Const == type)
                {
                    if (ifLeft)
                    {
                        prev.Left = node;
                    }
                    else
                    {
                        prev.Right = node;
                    }
                }
            }
            q.Enqueue(start);
            while (q.Count != 0) //替换节点 指向声明节点
            {
                var i = q.Dequeue();
                if (i == null)
                {
                    continue;
                }
                if (i.Left?.Type == ExprType.UnknownID || i.Left?.Type == ExprType.Var)
                {
                    var node = env.Find((string)i.Left.Info);
                    var type = node?.Type;
                    if (type == null)
                    {
                        ErrorMsg.Add($"Unknown Token '{i.Left.Info}',it needs declaring", i.Location);
                    }
                    else if (ExprType.Var == type)
                    {
                        i.Left = node;
                    }
                    else if (ExprType.ProcDefine == type)
                    {
                        ErrorMsg.Add($"Procedure name '{node.Info}' can't be part of expression", i.Location);
                    }
                    else if (ExprType.Const == type)
                    {
                        i.Left = node;
                    }
                }
                if (i.Right?.Type == ExprType.UnknownID || i.Right?.Type == ExprType.Var)
                {
                    var node = env.Find((string)i.Right.Info);
                    var type = node?.Type;
                    if (type == null)
                    {
                        ErrorMsg.Add($"Unknown Token '{i.Info}',it needs declaring", i.Location);
                    }
                    else if (ExprType.Var == type)
                    {
                        i.Right = node;
                    }
                    else if (ExprType.ProcDefine == type)
                    {
                        ErrorMsg.Add($"Procedure name '{node.Info}' can't be part of expression", i.Location);
                    }
                    else if (ExprType.Const == type)
                    {
                        i.Right = node;
                    }
                }
                q.Enqueue(i.Left);
                q.Enqueue(i.Right);
            }
        }

        private void Determine(Token token, Func<Token, bool> Condition, ref AstNode LinkedTo, Func<AstNode> Define)
        {
            if (token == null) return;
            try
            {
                if (Condition(token)) LinkedTo = Define();
            }
            catch (SyntaxErrorException e)
            {
                ErrorMsg.Add(e);
            }
        }

        private void CheckExpectToken(Token Expect) //检查当前Token是否为期望的，是则跳过该token，否则抛出异常
        {
            if (CurrentToken() == null || Expect != null && CurrentToken() != Expect)
            {
                throw new SyntaxErrorException($"Unexpected Token '{CurrentToken()?.Content}' ,Miss Expected tokens '{Expect?.Content}'",
                    (CurrentToken() == null ? new Position(-1, 0) : CurrentToken()?.Location));
            }
            if (!tokens.MoveNext())
            {
                throw new Exception();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Token CurrentToken()
        {
            try
            {
                return tokens.Current;
            }
            catch
            {
                return null;
            }
        }

        private void SkipErrorTokens()
        {
            while (CurrentToken() != null && (SkipControlList.IndexOf(CurrentToken()) == -1
                && (!(CurrentToken().Content is string) || !Keys.Contains((string)CurrentToken().Content))))
            {
                if (!tokens.MoveNext())
                {
                    break;
                }
            }
        }

        private IEnumerator<Token> tokens;
        public ErrorMsgList ErrorMsg;
        private HashSet<string> Keys;
        private List<Token> SkipControlList;
        private AstNode AstTree;
        #endregion
    }

    public enum ExprType
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

    public class Env
    {
        private Env prev;
        private Dictionary<string, AstNode> dict;
        public static HashSet<string> Keys;
        public static void Initial()
        {
            Keys = new HashSet<string>(new string[] { "procedure", "if", "then", "else", "while", "do", "call", "begin", "repeat", "until", "read", "write", "var", "const", "end", "odd" });
        }
        public Env(Env prevEnv)
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
        public int Reserve(AstNode node)
        {
            if (node.Type == ExprType.Var || node.Type == ExprType.Const || node.Type == ExprType.ProcDefine)
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

        public AstNode Find(string ID)
        {
            for (var e = this; e != null; e = e.prev)
            {
                if (e.dict.ContainsKey(ID)) return e.dict[ID];
            }
            return null;
        }
        public AstNode FindNoRecursion(string ID)
        {
            if (dict.ContainsKey(ID)) return dict[ID];
            return null;
        }
    }
}