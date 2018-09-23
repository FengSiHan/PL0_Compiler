using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    //双向链表
    public class DList<T> //where T : IEqualityComparer
    {
        internal class Node
        {
            internal T Data;
            internal Node Prev, Next;
        }

        private Node Head, Tail;
        public int Count { get; private set; }
        public DList()
        {
            Head = new Node();
            Tail = Head;
            Head.Next = Head.Prev = Head;
            Count = 0;
        }
        public void AddToEnd(T data)
        {
            Node node = new Node
            {
                Data = data
            };
            Insert(Tail, node);
            Tail = node;
            Count++;
        }
        public T[] GetData()
        {
            T[] list = new T[Count];
            Node node = Head.Next;
            int index = 0;
            while (node != Head)
            {
                list[index++] = node.Data;
                node = node.Next;
            }
            return list;
        }
        public void ForEach(Action<T> action)
        {
            Node node = Head.Next;
            while (node != Head)
            {
                action(node.Data);
                node = node.Next;
            }
        }


        /// <summary>
        /// 插入到第index个
        /// <summary>
        /// <param name="index">插入位置,从零开始</param>
        /// <param name="data">插入数据</param>
        public void InsertAt(int index, T data)
        {
            Node newNode = new Node();
            newNode.Data = data;
            Insert(Locate(index - 1), newNode);
            Count++;
        }
        /// <summary>
        /// 移除第index个元素
        /// </summary>
        /// <param name="index">移除数据位置，从0开始</param>
        public void RemoveAt(int index)
        {
            Node prev = Locate(index - 1);
            prev.Next.Next.Prev = prev;
            prev.Next = prev.Next.Next;
        }
        public bool Remove(T data)
        {
            Node node = Head.Next, prev = Head;
            while (node != Head)
            {
                if (node.Data.Equals(data))
                {
                    node.Next.Prev = prev;
                    prev.Next = node.Next;
                    return true;
                }
                else
                {
                    prev = node;
                    node = node.Next;
                }
            }
            return false;
        }
        private void Insert(Node left, Node right)
        {
            right.Next = left.Next;
            right.Prev = left;
            left.Next.Prev = right;
            left.Next = right;
        }
        private Node Locate(int index)
        {
            if (index >= Count || index < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            Node res = Head.Next;
            while (index-- != 0)
            {
                res = res.Next;
            }
            return res;
        }
    }
}