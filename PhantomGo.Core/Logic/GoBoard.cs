using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using PhantomGo.Core.Models;

namespace PhantomGo.Core.Logic
{
    /// <summary>
    /// 代表围棋棋盘，封装游戏的核心规则
    /// </summary>
    public class GoBoard
    {
        private readonly PointState[,] _board;
        private Point? _koPoint; // 禁着点
        public int Size { get; private set; }
        public GoBoard(int size = 9)
        {
            Size = size;
            _board = new PointState[Size + 1, Size + 1];
        }
        /// <summary>
        /// 获取坐标点的状态
        /// </summary>
        public PointState GetPointState(Point point)
        {
            return _board[point.X, point.Y];
        }
        /// <summary>
        /// 尝试落子
        /// </summary>
        /// <param name="point">落子位置</param>
        /// <param name="player">执子玩家</param>
        /// <returns>落子结果</returns>
        public PlayResult PlaceStone(Point point, Player player)
        {
            // 检查坐标是否在棋盘内、是否为无子状态、是否落在劫争禁着点内
            if (!IsOnBoard(point) || GetPointState(point) != PointState.None || _koPoint.HasValue && point.Equals(_koPoint.Value))
            {
                return PlayResult.Failure("该落子位置不合法");
            }

            // 没有落在禁着点内，下一手重置禁着点信息
            _koPoint = null;
            // 尝试落子
            var stoneColor = PlayerToPointState(player); // 获取当前执棋者颜色
            _board[point.X, point.Y] = stoneColor;

            // 检查是否可以提子
            var capturedStones = new List<Point>();
            var opponent = PlayerHelper.GetOpponent(player);
            foreach(var neighbor in GetNeighbors(point))
            {
                if(GetPointState(neighbor) == PlayerToPointState(opponent))
                {
                    var (group, liberties) = FindGroup(neighbor);
                    if(liberties == 0)
                    {
                        capturedStones.AddRange(group);
                    }
                }
            }
            if(capturedStones.Count > 0)
            {
                RemoveGroup(capturedStones);
                // 当提子数为 1 时，判断是否形成劫
                if(capturedStones.Count == 1)
                {
                    _koPoint = capturedStones.First();
                }
            }

            // 检查自杀规则（检查落子位置的气，若气为 0 且没有提子的情况下，判定为自杀）
            var (newGroup, newLiberties) = FindGroup(point);
            if(capturedStones.Count == 0 && newLiberties == 0)
            {
                _board[point.X, point.Y] = PointState.None; // 撤销落子
                return PlayResult.Failure("该落子位置不合法");
            }

            // 移动有效
            return PlayResult.Success(capturedStones.Distinct().ToList(), String.Empty);
        }

        #region 辅助方法
        public int GetLiberty(Point point)
        {
            int liberty = FindGroup(point).liberties;
            return liberty;
        }
        /// <summary>
        /// 检查一个落子是否合法（不修改当前棋盘状态）
        /// </summary>
        /// <param name="point">落子点</param>
        /// <param name="player">下棋的玩家</param>
        /// <returns>如果合法返回 true，否则返回 false</returns>
        public bool IsValidMove(Point point, Player player)
        {
            GoBoard tempBoard = this.Clone();
            return tempBoard.PlaceStone(point, player).IsSuccess;
        }
        /// <summary>
        /// 创建当前棋盘对象的副本
        /// </summary>
        public GoBoard Clone()
        {
            var newBoard = new GoBoard(this.Size);
            Array.Copy(this._board, newBoard._board, this._board.Length);
            newBoard._koPoint = this._koPoint;
            return newBoard;
        }
        /// <summary>
        /// 检查一个坐标是否在棋盘内
        /// </summary>
        private bool IsOnBoard(Point point)
        {
            return point.X <= Size && point.X > 0 &&
                   point.Y <= Size && point.Y > 0;
        }
        /// <summary>
        /// 获取一个点的所有有效邻居
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public IEnumerable<Point> GetNeighbors(Point point)
        {
            if(point.X > 1) yield return new Point(point.X - 1, point.Y);  
            if(point.X < Size) yield return new Point(point.X + 1, point.Y);
            if(point.Y > 1) yield return new Point(point.X, point.Y - 1);
            if(point.Y < Size) yield return new Point(point.X, point.Y + 1);
        }
        /// <summary>
        /// 使用 BFS 从一个点开始搜索相连的棋子群，并计算它们的气
        /// </summary>
        /// <returns>一个元组，包含棋子群的所有点和一个整数表示气的数量</returns>
        private (HashSet<Point> group, int liberties) FindGroup(Point startPoint)
        {
            // 获取起点状态，如果无子，直接返回
            var state = GetPointState(startPoint); 
            if(state == PointState.None)
            {
                return (new HashSet<Point>(), 0);
            }
            var group = new HashSet<Point>();
            var liberties = new HashSet<Point>();
            var queue = new Queue<Point>();
            var visited = new HashSet<Point>();
            queue.Enqueue(startPoint);
            visited.Add(startPoint);
            while(queue.Count > 0)
            {
                var current = queue.Dequeue();
                group.Add(current);
                // 遍历当前棋子的每个邻居
                foreach(var neighbor in GetNeighbors(current))
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    var neighborState = GetPointState(neighbor);
                    // 邻居状态与当前棋子状态一样，加入棋子群中
                    if(neighborState == state)
                    {
                        queue.Enqueue(neighbor);
                    } else if(neighborState == PointState.None) // 邻居无子，说明是当前棋子的气
                    {
                        liberties.Add(neighbor);
                    }
                }
            }
            return (group, liberties.Count);
        }
        /// <summary>
        /// 从棋盘上移除棋子群，将它们的状态设置为无子
        /// </summary>
        private void RemoveGroup(IEnumerable<Point> group)
        {
            foreach(var point in group)
            {
                _board[point.X, point.Y] = PointState.None;
            }
        }
        /// <summary>
        /// 从棋盘上恢复棋子群
        /// </summary>
        private void AddGroup(IEnumerable<Point> group, Player player)
        {
            var state = PlayerToPointState(player);
            foreach(var point in group)
            {
                _board[point.X, point.Y] = state;
            }
        }
        /// <summary>
        /// 计算当前棋盘状态的哈希字符串
        /// </summary>
        /// <returns></returns>
        private string CalculateBoardStateHash()
        {
            var sb = new StringBuilder(Size * Size);
            for(int x = 1;x <= Size;++x)
            {
                for(int y = 1;y <= Size;++y)
                {
                    sb.Append((int)_board[x, y]);
                }
            }
            return sb.ToString();
        }
        /// <summary>
        /// 将 Player 枚举转换为 PointState 枚举
        /// </summary>
        private PointState PlayerToPointState(Player player)
        {
            return player == Player.Black ? PointState.black : PointState.white;
        }
        #endregion
    }
}
