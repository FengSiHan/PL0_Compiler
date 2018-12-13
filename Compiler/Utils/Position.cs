using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class Position : ObservableCollection<Position>, IComparable
    {
        public int Row { get; set; }
        public int Col { get; set; }

        public Position() { }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public override string ToString()
        {
            return $"At Row {Row},Col {Col}";
        }

        public override int GetHashCode()
        {
            return Row.GetHashCode() ^ Col.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Position loc = obj as Position;
            if (ReferenceEquals(loc, null))
            {
                return false;
            }
            return loc.Col == Col && loc.Row == Row;
        }

        public int CompareTo(object obj)
        {
            if (!(obj is Position))
            {
                //throw new ArgumentException();
                return 1;
            }
            Position pos = obj as Position;
            if (pos.Row > Row)
            {
                return -1;
            }
            else if (pos.Row < Row)
            {
                return 1;
            }
            else if (pos.Col > Col)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
    }
}
