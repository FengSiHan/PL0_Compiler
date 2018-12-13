using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class ErrorMsgList
    {
        public List<ErrorInfo> Errors { get; }

        public ErrorMsgList(int MaxErrors)
        {
            Errors = new List<ErrorInfo>(MaxErrors);
        }

        public void Add(string msg, int row, int col)
        {
            Errors.Add(new ErrorInfo(msg, row, col));
        }

        public void Add(string msg, Position pos)
        {
            Errors.Add(new ErrorInfo(msg, pos));
        }

        public IEnumerator<ErrorInfo> GetEnumerator()
        {
            return Errors.GetEnumerator();
        }

        public void Add(SyntaxErrorException e)
        {
            Add(e.Message, e.Location);
        }

        public string GetErrorMsgString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var i in Errors)
            {
                sb.Append(i).Append('\n');
            }
            return sb.ToString();
        }

        public void Clear()
        {
            Errors.Clear();
        }


        public void SortErrorMsgByLine()
        {
            try
            {
                Errors.Sort();

            }
            catch
            {

            }
        }

        public int Count()
        {
            return Errors.Count;
        }
    }

    public class ErrorInfo : ObservableCollection<ErrorInfo>, IComparable
    {
        public string Message { get; set; }

        public Position Location { get; set; }

        public ErrorInfo(string msg, int row, int col)
        {
            Message = msg;
            Location = new Position(row, col);
        }

        public ErrorInfo(string msg, Position pos)
        {
            Message = msg;
            Location = pos;
        }

        public int CompareTo(object obj)
        {
            ErrorInfo e = obj as ErrorInfo;
            if (e == null)
                return -1;
            return this.Location.CompareTo(e.Location);
        }

        public override string ToString()
        {
            if (Location?.Row == -1) return Message + ", At the end of code";
            return Message + ", " + Location;
        }
    }
}