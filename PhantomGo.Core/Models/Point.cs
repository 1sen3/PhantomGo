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
            return $"({X}, {Y})";
        }
    }
}
