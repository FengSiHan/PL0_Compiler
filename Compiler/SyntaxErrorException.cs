using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class SyntaxErrorException : Exception
    {
        public Position Location;
        public SyntaxErrorException(string msg, Position pos) : base(msg)
        {
            Location = pos;
        }
    }
}
