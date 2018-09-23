﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class ErrorMsgList
    {
        private List<ErrorInfo> Errors { get; }
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
        public void PrintErrorMsg()
        {
            foreach (var i in Errors)
            {
                Console.WriteLine(i);
            }
        }
        public void SortErrorMsgByLine()
        {
            Errors.Sort();
        }
        public int Count()
        {
            return Errors.Count;
        }
    }
    public class ErrorInfo : IComparable
    {
        public string Message;
        public Position Location;
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
            if (Location.Row == -1) return Message + ", At the end of code";
            return Message + ", " + Location;
        }
    }
}