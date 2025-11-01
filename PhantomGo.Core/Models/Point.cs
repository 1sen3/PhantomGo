using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示棋盘上一个二维坐标 (row, col)
    /// 注意：构造函数参数顺序为 (行, 列)，与数组索引 [row, col] 一致
    /// </summary>
    public readonly struct Point : IEquatable<Point>
    {
        /// <summary>
        /// 行坐标（垂直方向，1-9，显示时倒序为 9-1）
        /// </summary>
        public int Row { get; }
        /// <summary>
        /// 列坐标（水平方向，1-9 对应 A-I）
        /// </summary>
        public int Col { get; }

        public Point(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(Point other)
        {
            return Row == other.Row && Col == other.Col;
        }
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is Point other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Col);
        }

        public static bool operator ==(Point left, Point right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point left, Point right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            char symbol = (char)('A' + Col - 1);
            return $"{symbol}{10 - Row}";
        }
        /// <summary>
        /// 将棋局上的坐标 (A1) 转换为坐标点 (row, col)
        /// 例如：A9 -> Point(1, 1)，I1 -> Point(9, 9)
        /// </summary>
        public static Point TransInputToPoint(string input)
        {
            if(string.IsNullOrEmpty(input) || input.Length != 2)
            {
                throw new ArgumentException("输入格式错误");
            }
            input = input.ToUpper();
            char symbol = input[0];
            int col = symbol - 'A' + 1;
            int row = 10 - int.Parse(input.Substring(1));
            return new Point(row, col);
        }
        public bool isMove()
        {
            return !(this.Equals(new Point(0, 0)) || this.Equals(new Point(0, 1)) || this.Equals(new Point(0, 2)));
        }
        public bool IsPass()
        {
            return this.Row == 0 && this.Col == 0;

        }
        public static Point Pass()
        {
            return new Point(0, 0);
        }
        public static Point Undo()
        {
            return new Point(0, 1);
        }

        public static Point Quit()
        {
            return new Point(0, 2);
        }
        public static Point Unlegal()
        {
            return new Point(10, 10);
        }
    }
}
