using System;
using System.Collections.Generic;

namespace Compiler
{
    //由于对LINQ效率持怀疑态度，故写了这个类
    public static class SetOperation
    {
        public static List<T> Intersect<T>(this List<T> arg1, List<T> arg2)
        {
            if (arg1.Count < arg2.Count)
            {
                var tmp = arg1;
                arg1 = arg2;
                arg2 = tmp;
            }
            HashSet<T> set = new HashSet<T>(arg1);
            List<T> result = new List<T>();
            foreach (var i in arg2)
            {
                if (set.Contains(i))
                {
                    result.Add(i);
                }
            }
            return result;
        }
        public static List<T> Union<T>(this List<T> arg1, List<T> arg2)
        {
            HashSet<T> set = new HashSet<T>(arg1);
            foreach (var i in arg2)
            {
                set.Add(i);
            }
            return new List<T>(set);
        }
        public static List<T> Distinct<T>(this List<T> arg)
        {
            return new List<T>(new HashSet<T>(arg));
        }
        public static List<T> Distinct<T>(this List<T> arg1, List<T> arg2)
        {
            HashSet<T> set = new HashSet<T>(arg1);
            List<T> diff = new List<T>();
            foreach (var i in arg2)
            {
                if (set.Contains(i) == false)
                {
                    diff.Add(i);
                }
            }
            return diff;
        }
        /// <summary>
        /// 是否有不同元素成员,有则返回true，无则false
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static bool Different<T>(this List<T> arg1, List<T> arg2)
        {
            HashSet<T> set = new HashSet<T>(arg1);
            List<T> diff = new List<T>();
            foreach (var i in arg2)
            {
                if (set.Contains(i) == false)
                {
                    return true;
                }
            }
            return false;
        }
        public static List<T> Except<T>(this List<T> arg1, List<T> arg2)
        {
            HashSet<T> set = new HashSet<T>(arg1);
            foreach (var i in arg2)
            {
                if (set.Contains(i))
                {
                    set.Remove(i);
                }
            }
            return new List<T>(set);
        }
        public static bool ElementEqual<T>(this List<T> arg1, List<T> arg2, Func<T, T, int> cmp = null) where T : IEquatable<T>
        {
            if (arg1.Count != arg2.Count)
            {
                return false;
            }
            if (cmp != null)
            {
                arg1.Sort(new Comparison<T>(cmp));
                arg2.Sort(new Comparison<T>(cmp));
            }
            else
            {
                arg1.Sort();
                arg2.Sort();
            }
            for (int i = 0; i < arg1.Count; ++i)
            {
                if (arg1[i].GetHashCode() != arg2[i].GetHashCode())
                {
                    return false;
                }
                else if (false == arg1[i].Equals(arg2[i]))
                {
                    return false;
                }
            }
            return true;
        }
        public static List<T> ToList<T>(this List<T> list)
        {
            return new List<T>(list);
        }
        /// <summary>
        /// 返回arg1和arg2的关系（参数中不能有重复元素）
        /// 1为arg1真包含arg2, 2为arg2真包含arg1,3为相等 4为部分交叉或者独立
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static int Contain<T>(this List<T> arg1, List<T> arg2) where T : IEquatable<T>//arg1,arg2中不能出现重复的
        {
            if (arg1.Count < arg2.Count)
            {
                if (arg2.Different(arg1))
                {
                    return 4;
                }
                else
                {
                    return 2;
                }
            }
            else if (arg1.Count == arg2.Count)
            {
                if (arg1.ElementEqual(arg2))
                {
                    return 3;
                }
                else
                {
                    return 4;
                }
            }
            else
            {
                if (arg1.Different(arg2))
                {
                    return 4;
                }
                else
                {
                    return 1;
                }
            }
        }
    }
}
