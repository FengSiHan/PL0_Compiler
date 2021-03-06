﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class Enumerator<T>
    {
        private IEnumerator<T> Data;
        private bool MoreData;
        private T Last;
        public Enumerator(IEnumerator<T> data)
        {
            Data = data;
        }
        public bool MoveNext()
        {
            MoreData = Data.MoveNext();
            return MoreData;
        }
        public T Current()
        {
            if (MoreData)
            {
                Last = Data.Current;
                return Data.Current;
            }
            else
            {
                return default(T);
            }
        }
        public T GetLast()
        {
            return Last;
        }
    }
}
