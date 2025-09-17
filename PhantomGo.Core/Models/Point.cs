using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示棋盘上一个二维坐标 (x,y)
    /// </summary>
    public readonly struct Point : IEquatable<Point>
    {
        /// <summary>
        /// x 轴坐标
        /// </summary>
        public int X { get; }
        /// <summary>
        /// y 轴坐标
        /// </summary>
        public int Y { get; }
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Point other)
        {
            return X == other.X && Y == other.Y;
        }
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is Point other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
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
            char symbol = (char)('A' + X - 1);
            return $"{symbol}{Y}";
        }
        /// <summary>
        /// 将棋局上的坐标 (A1) 转换为坐标点 (x, y)
        /// </summary>
        public static Point TransInputToPoint(string input)
        {
            if(string.IsNullOrEmpty(input) || input.Length != 2)
            {
                throw new ArgumentException("输入格式错误");
            }
            input = input.ToUpper();
            char symbol = input[0];
            int x = symbol - 'A' + 1;
            int y = int.Parse(input.Substring(1));
            return new Point(x, y);
        }
        public bool isMove()
        {
            return !(this.Equals(new Point(0, 0)) || this.Equals(new Point(0, 1)) || this.Equals(new Point(0, 2)));
        }
    }
}
