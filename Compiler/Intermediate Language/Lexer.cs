using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler
{
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
    public class Lexer
    {
        public IEnumerable<Token> Scan()
        {
            char peek;
            do
            {
                peek = chars.Current;
                dont_move = false;
                if (char.IsWhiteSpace(peek))
                {
                    continue;
                }
                else if (peek == ',')
                {
                    yield return new Token(TokenType.COMMA, ',', Row, Col);
                }
                else if ("+-/*".IndexOf(peek) != -1)
                {
                    yield return new Token(TokenType.OP, peek, Row, Col);
                }
                else if ("()".IndexOf(peek) != -1)
                {
                    yield return new Token(TokenType.BRACKET, peek, Row, Col);
                }
                else if (Char.IsDigit(peek))
                {
                    yield return new Token(TokenType.NUM, GetSubstringByCond(peek, Char.IsDigit), Row, Col);
                }
                else if (Char.IsLetter(peek))
                {
                    yield return new Token(TokenType.ID, GetSubstringByCond(peek, Char.IsLetterOrDigit), Row, Col);
                }
                else if (peek == '=')
                {
                    yield return new Token(TokenType.OP, peek, Row, Col);
                }
                else if (peek == ';')
                {
                    yield return new Token(TokenType.SEMICOLON, peek, Row, Col);
                }
                else if (peek == ':')
                {
                    if (MoveNext() && chars.Current == '=')
                    {
                        yield return new Token(TokenType.ASSIGN, ":=", Row, Col - 1);
                    }
                    else
                    {
                        ErrorMsg.Add("Missing '=' after ':' ", Row, Col); // Or:   dontmove = true; Ignore this mistake
                    }

                }
                else if (peek == '<')
                {
                    if (MoveNext())
                    {
                        if (chars.Current == '=')
                        {
                            yield return new Token(TokenType.OP, "<=", Row, Col - 1);
                        }
                        else if (chars.Current == '>')
                        {
                            yield return new Token(TokenType.OP, "<>", Row, Col - 1);
                        }
                        else
                        {
                            dont_move = true;
                            yield return new Token(TokenType.OP, '<', Row, Col);
                        }
                    }
                    else
                    {
                        yield return new Token(TokenType.OP, '<', Row, Col);
                    }
                }
                else if (peek == '>')
                {
                    if (MoveNext())
                    {
                        if (chars.Current == '=')
                        {
                            yield return new Token(TokenType.OP, ">=", Row, Col - 1);
                        }
                        else
                        {
                            dont_move = true;
                            yield return new Token(TokenType.OP, '>', Row, Col);
                        }
                    }
                    else
                    {
                        yield return new Token(TokenType.OP, '>', Row, Col);
                    }
                }
                else if (peek == '.')
                {
                    yield return new Token(TokenType.PERIOD, '.', Row, Col);
                }
                else
                {
                    string tmp = "";
                    int col = Col;
                    while (!char.IsWhiteSpace(peek))
                    {
                        tmp += peek;
                        if (MoveNext()) break;
                        peek = chars.Current;
                    }
                    ErrorMsg.Add($"Unknown Token '{tmp}'", Row, Col);
                }

                //else if ("\"\'".IndexOf(peek) != -1) yield return new Token(TokenType.STRING, (object)TODO, Line); //PL0 doesn't have string
            }
            while (dont_move || MoveNext()); //'dont_move' can avoid too much MoveNext()

        }

        public Lexer(IEnumerable<char> text)
        {
            Text = text;
            Row = 1;
            Col = 0;
            chars = Text.GetEnumerator();
            dont_move = true;
            ErrorMsg = new ErrorMsgList(MaxErrors);
            if (MoveNext() == false)
            {
                throw new SyntaxErrorException("Text is null", new Position(0, 0));
            }
        }

        private bool MoveNext()
        {
            if (!chars.MoveNext())
            {
                return false;
            }
            else if (chars.Current == '\n')
            {
                ++Row;
                Col = 0;
            }
            else
            {
                ++Col;
            }
            return true;
        }

        private string GetSubstringByCond(char peek, ConditionFunc Condition)//Condition is a regex pattern string
        {
            StringBuilder str = new StringBuilder();
            str.Append(peek);
            while (MoveNext())
            {
                if (Condition(chars.Current)) str.Append(chars.Current);
                else
                {
                    dont_move = true;
                    return str.ToString();
                }
            }
            dont_move = false;
            return str.ToString();
        }

        private delegate bool ConditionFunc(char c);
        private readonly IEnumerable<char> Text;
        private int Row, Col;
        private bool dont_move;
        IEnumerator<char> chars;
        public ErrorMsgList ErrorMsg;
        private readonly int MaxErrors = 1024;
    }

    public class Token
    {
        public TokenType TokenType;
        public object Content;
        public Position Location;
        public Token(TokenType type, object content, int row = 0, int col = 0)
        {
            TokenType = type;
            Content = content;
            Location = new Position(row, col);
        }
        public static bool operator ==(Token t1, Token t2)
        {
            if (ReferenceEquals(t1, null) || ReferenceEquals(t2, null))
            {
                return false;
            }
            if (t1.TokenType != t2.TokenType)
            {
                return false;
            }
            switch (t1.TokenType)
            {
                case TokenType.ASSIGN:
                case TokenType.SEMICOLON:
                case TokenType.PERIOD:
                case TokenType.COMMA:
                    return true;
                case TokenType.NUM:
                case TokenType.ID:
                case TokenType.STRING:
                    return (string)t1.Content == (string)t2.Content;
                case TokenType.OP:
                    {
                        if (t1.Content.GetType() != t2.Content.GetType())
                        {
                            return false;
                        }
                        if (t1.Content.GetType().Name == "Char")
                        {
                            return (char)t1.Content == (char)t2.Content;
                        }
                        else
                        {
                            return (string)t1.Content == (string)t2.Content;
                        }
                    }
                case TokenType.BRACKET:
                    return (char)t1.Content == (char)t2.Content;
                default:
                    return false;
            }
        }
        public static bool operator !=(Token t1, Token t2)
        {
            return !(t1 == t2);
        }
        public override bool Equals(object obj)
        {
            Token t = obj as Token;
            return t == this;
        }
        public override int GetHashCode()
        {
            return Content == null ? 0 : Content.GetHashCode() ^ Location.GetHashCode() ^ TokenType.GetHashCode();
        }
        public static readonly Token WHILE = new Token(TokenType.ID, "while"),
            DO = new Token(TokenType.ID, "do"),
            IF = new Token(TokenType.ID, "if"),
            ELSE = new Token(TokenType.ID, "else"),
            THEN = new Token(TokenType.ID, "then"),
            CALL = new Token(TokenType.ID, "call"),
            READ = new Token(TokenType.ID, "read"),
            WRITE = new Token(TokenType.ID, "write"),
            LBRACKET = new Token(TokenType.BRACKET, '('),
            RBRACKET = new Token(TokenType.BRACKET, ')'),
            BEGIN = new Token(TokenType.ID, "begin"),
            END = new Token(TokenType.ID, "end"),
            REPEAT = new Token(TokenType.ID, "repeat"),
            UNTIL = new Token(TokenType.ID, "until"),
            SEMICOLON = new Token(TokenType.SEMICOLON, ';'),
            PERIOD = new Token(TokenType.PERIOD, '.'),
            VAR = new Token(TokenType.ID, "var"),
            CONST = new Token(TokenType.ID, "const"),
            PROC = new Token(TokenType.ID, "procedure"),
            ASSIGN = new Token(TokenType.ASSIGN, ":="),
            COMMA = new Token(TokenType.COMMA, ',');
    }
}