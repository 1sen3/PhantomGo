using Microsoft.VisualBasic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Logic
{
    /// <summary>
    /// 代表围棋棋盘，封装游戏的核心规则
    /// </summary>
    public class GoBoard
    {
        private readonly PointState[,] _board;
        private Point? _koPoint; // 禁着点

        // Zobrist Hash
        private ulong _currentHash;
        private static readonly ZobristHash _zobrist;
        private readonly HashSet<ulong> _historyHashes; // 检测全局同形

        public int Size { get; private set; }
        static GoBoard()
        {
            _zobrist = new ZobristHash();
        }
        public GoBoard()
        {
            Size = 9;
            _board = new PointState[Size + 1, Size + 1];
            _currentHash = 0;
            _historyHashes = new HashSet<ulong>();
            _historyHashes.Add(_currentHash);
        }
        public ulong GetCurrentHash() => _currentHash;

        public PointState GetPointState(Point point)
        {
            return _board[point.X, point.Y];
        }

        public PlayResult PlaceStone(Point point, Player player)
        {
            if (!IsOnBoard(point) || GetPointState(point) != PointState.None || (_koPoint.HasValue && point.Equals(_koPoint.Value)))
            {
                return PlayResult.Failure("该落子位置不合法");
            }

            var stoneColor = PlayerToPointState(player);
            _board[point.X, point.Y] = stoneColor;

            // 提子逻辑
            var capturedStones = new List<Point>();
            var opponent = PlayerToPointState(player.GetOpponent());
            foreach (var neighbor in GetNeighbors(point))
            {
                if (GetPointState(neighbor) == opponent)
                {
                    var (group, liberties) = FindGroup(neighbor);
                    if (liberties == 0)
                    {
                        capturedStones.AddRange(group);
                    }
                }
            }

            var (ownGroup, ownLiberties) = FindGroup(point);
            if (capturedStones.Count == 0 && ownLiberties == 0)
            {
                _board[point.X, point.Y] = PointState.None;
                return PlayResult.Failure("自杀点，禁止落子");
            }

            ulong nextHash = _currentHash;
            nextHash ^= _zobrist.GetHash(point.X, point.Y, (int)stoneColor);
            foreach(var captured in capturedStones.Distinct())
            {
                nextHash ^= _zobrist.GetHash(captured.X, captured.Y, (int)opponent);
            }
            if (_historyHashes.Contains(nextHash))
            {
                _board[point.X, point.Y] = PointState.None;
                return PlayResult.Failure("全局同形，禁止落子");
            }

            _currentHash = nextHash;
            _historyHashes.Add(_currentHash);
            _koPoint = null;

            if (capturedStones.Count > 0)
            {
                RemoveGroup(capturedStones.Distinct().ToList(), false);
                // 当提子数为 1 时，判断是否形成劫
                if (capturedStones.Count == 1)
                {
                    // 进一步判断是否是真的劫（即提子后我方棋子是否只有一口气）
                    if (capturedStones.Count == 1 && ownGroup.Count == 1 && ownLiberties == 1)
                    {
                        _koPoint = capturedStones.First();
                    }
                }
            }

            return PlayResult.Success(capturedStones.Distinct().ToList(), String.Empty);
        }

        #region 辅助方法
        public bool IsValidMove(Point point, Player player)
        {
            if(!IsOnBoard(point) || GetPointState(point) != PointState.None || (_koPoint.HasValue && _koPoint.Value.Equals(point))) {
                return false;
            }

            GoBoard tmpBoard = this.CloneForValidation();
            return tmpBoard.PlaceStone(point, player).IsSuccess;
        }

        private GoBoard CloneForValidation()
        {
            var newBoard = new GoBoard();
            Array.Copy(this._board, newBoard._board, this._board.Length);
            newBoard._koPoint = this._koPoint;
            newBoard._currentHash = this._currentHash;
            foreach (var hash in this._historyHashes)
            {
                newBoard._historyHashes.Add(hash);
            }
            return newBoard;
        }

        public GoBoard Clone()
        {
            var newBoard = new GoBoard();
            Array.Copy(this._board, newBoard._board, this._board.Length);
            newBoard._koPoint = this._koPoint;
            newBoard._currentHash = this._currentHash;
            foreach (var hash in this._historyHashes)
            {
                newBoard._historyHashes.Add(hash);
            }
            return newBoard;
        }
        private bool IsOnBoard(Point point)
        {
            return point.X <= Size && point.X > 0 &&
                   point.Y <= Size && point.Y > 0;
        }

        public IEnumerable<Point> GetNeighbors(Point point)
        {
            if (point.X > 1) yield return new Point(point.X - 1, point.Y);
            if (point.X < Size) yield return new Point(point.X + 1, point.Y);
            if (point.Y > 1) yield return new Point(point.X, point.Y - 1);
            if (point.Y < Size) yield return new Point(point.X, point.Y + 1);
        }

        public int GetLiberty(Point point)
        {
            int liberty = FindGroup(point).liberties;
            return liberty;
        }

        private (HashSet<Point> group, int liberties) FindGroup(Point startPoint)
        {
            // 获取起点状态，如果无子，直接返回
            var state = GetPointState(startPoint);
            if (state == PointState.None)
            {
                return (new HashSet<Point>(), 0);
            }
            var group = new HashSet<Point>();
            var liberties = new HashSet<Point>();
            var queue = new Queue<Point>();
            var visited = new HashSet<Point>();
            queue.Enqueue(startPoint);
            visited.Add(startPoint);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                group.Add(current);
                // 遍历当前棋子的每个邻居
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    var neighborState = GetPointState(neighbor);
                    // 邻居状态与当前棋子状态一样，加入棋子群中
                    if (neighborState == state)
                    {
                        queue.Enqueue(neighbor);
                    } else if (neighborState == PointState.None) // 邻居无子，说明是当前棋子的气
                    {
                        liberties.Add(neighbor);
                    }
                }
            }
            return (group, liberties.Count);
        }

        private void RemoveGroup(IEnumerable<Point> group, bool updateHash = true)
        {
            foreach (var point in group)
            {
                if (updateHash)
                {
                    // 撤销哈希就是把对应的key再异或一次
                    _currentHash ^= _zobrist.GetHash(point.X, point.Y, (int)_board[point.X, point.Y]);
                }
                _board[point.X, point.Y] = PointState.None;
            }
        }
        public List<Point> GetEmptyPoints()
        {
            var points = new List<Point>();
            for (int x = 1; x <= Size; ++x)
            {
                for (int y = 1; y <= Size; ++y)
                {
                    var point = new Point(x, y);
                    if (GetPointState(point) == PointState.None) points.Add(point);
                }
            }
            return points;
        }

        private PointState PlayerToPointState(Player player)
        {
            return player == Player.Black ? PointState.black : PointState.white;
        }
        #endregion

        #region Zobrist Hash
        internal class ZobristHash
        {
            private readonly ulong[,,] _hashKeys; // [x, y, player]
            private readonly Random _random;

            public ZobristHash()
            {
                int size = 9;
                _random = new Random();
                _hashKeys = new ulong[size + 1, size + 1, 3]; // 0: None, 1: Black, 2: White

                for(int i = 0;i <= size; ++i)
                {
                    for(int j = 0;j <= size; ++j)
                    {
                        for(int k = 1; k <= 2;++k)
                        {
                            _hashKeys[i, j, k] = NextULong();
                        }
                    }
                }
            }

            public ulong GetHash(int x, int y, int player)
            {
                return _hashKeys[x, y, player];
            }
            private ulong NextULong()
            {
                byte[] buffer = new byte[8];
                _random.NextBytes(buffer);
                return BitConverter.ToUInt64(buffer, 0);
            }
        }
        #endregion
    }
}
