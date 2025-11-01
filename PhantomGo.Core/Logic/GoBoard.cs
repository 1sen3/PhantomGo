using Microsoft.VisualBasic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private static Dictionary<Point, List<Point>> NEIGHBORS_CACHE;
        private static Dictionary<Point, List<Point>> DIAGONALS_CACHE;

        // 历史棋盘状态记录 (用于神经网络特征提取)
        private readonly List<PointState[,]> _boardHistory;
        private const int MAX_HISTORY_LENGTH = 8; // 保留最近8步的历史

        public GameState GameState { get; set; }

        public record struct UndoInfo(Point Point, Point? PreviousKoPoint, List<Point> CapturedPoints, ulong PreviousHash);

        public int Size { get; private set; }
        static GoBoard()
        {
            _zobrist = new ZobristHash();
        }
        public GoBoard()
        {
            Size = 9;
            GameState = GameState.Playing;
            _board = new PointState[Size + 1, Size + 1];
            _currentHash = 0;
            _historyHashes = new HashSet<ulong>();
            _historyHashes.Add(_currentHash);
            _boardHistory = new List<PointState[,]>();
            // 初始化时添加一个空棋盘作为初始状态
            _boardHistory.Add(CloneBoardState(_board));
            NEIGHBORS_CACHE = InitializeNeighbors();
            DIAGONALS_CACHE = InitializeDiagonals();
        }
        public ulong GetCurrentHash() => _currentHash;

        public PointState GetPointState(Point point)
        {
            return _board[point.Row, point.Col];
        }

        public PlayResult PlaceStone(Point point, Player player)
        {
            if (!IsOnBoard(point) || GetPointState(point) != PointState.None || (_koPoint.HasValue && point.Equals(_koPoint.Value)))
            {
                return PlayResult.Failure("该落子位置不合法");
            }

            var stoneColor = PlayerToPointState(player);
            _board[point.Row, point.Col] = stoneColor;

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
                _board[point.Row, point.Col] = PointState.None;
                return PlayResult.Failure("自杀点，禁止落子");
            }

            ulong nextHash = _currentHash;
            nextHash ^= _zobrist.GetHash(point.Row, point.Col, (int)stoneColor);
            foreach(var captured in capturedStones.Distinct())
            {
                nextHash ^= _zobrist.GetHash(captured.Row, captured.Col, (int)opponent);
            }
            if (_historyHashes.Contains(nextHash))
            {
                _board[point.Row, point.Col] = PointState.None;
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

            // 记录当前棋盘状态到历史
            RecordBoardHistory();

            return PlayResult.Success(capturedStones.Distinct().ToList(), String.Empty);
        }
        public UndoInfo? PlaceStoneForSimulation(Point point, Player player)
        {
            if(!IsOnBoard(point) || GetPointState(point) != PointState.None || (_koPoint.HasValue && _koPoint.Value == point))
            {
                return null;
            }

            var previousHash = _currentHash;
            var previousKoPoint = _koPoint;

            var stoneColor = PlayerToPointState(player);
            _board[point.Row, point.Col] = stoneColor;

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
                _board[point.Row, point.Col] = PointState.None;
                return null;
            }

            ulong nextHash = _currentHash;
            nextHash ^= _zobrist.GetHash(point.Row, point.Col, (int)stoneColor);
            foreach (var captured in capturedStones.Distinct())
            {
                nextHash ^= _zobrist.GetHash(captured.Row, captured.Col, (int)opponent);
            }
            if (_historyHashes.Contains(nextHash))
            {
                _board[point.Row, point.Col] = PointState.None;
                return null;
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

            // 记录当前棋盘状态到历史
            RecordBoardHistory();

            return new UndoInfo(point, previousKoPoint, capturedStones.Distinct().ToList(), previousHash);
        }
        public void UndoMove(UndoInfo undoInfo, Player player)
        {
            _board[undoInfo.Point.Row, undoInfo.Point.Col] = PointState.None;

            var opponent = PlayerToPointState(player.GetOpponent());
            foreach(var point in undoInfo.CapturedPoints)
            {
                _board[point.Row, point.Col] = opponent;
            }

            _historyHashes.Remove(_currentHash);
            _currentHash = undoInfo.PreviousHash;
            _koPoint = undoInfo.PreviousKoPoint;

            // 撤销历史记录（移除最后一条）
            if (_boardHistory.Count > 1) // 至少保留一个初始状态
            {
                _boardHistory.RemoveAt(_boardHistory.Count - 1);
            }
        }

        #region 辅助方法
        public PointState IsKoish(Point point)
        {
            if (GetPointState(point) != PointState.None) return PointState.None;
            var neigborStateSet = new HashSet<PointState>();
            foreach(var neighbor in GetNeighbors(point))
            {
                neigborStateSet.Add(GetPointState(neighbor));
            }
            if(neigborStateSet.Count == 1 && !neigborStateSet.Contains(PointState.None))
            {
                return neigborStateSet.First();
            } else
            {
                return PointState.None;
            }
        }
        public PointState IsEyeish(Point point)
        {
            var color = IsKoish(point);
            if (color == PointState.None) return PointState.None;
            var diagonalFaults = 0;
            var diagonals = GetDiagonals(point);
            if (diagonals.Count < 4) diagonalFaults += 1;
            foreach(var diag in diagonals)
            {
                var diagState = GetPointState(diag);
                if (diagState != color && diagState != PointState.None) diagonalFaults += 1;
            }
            if (diagonalFaults > 1) return PointState.None;
            else return color;
        }
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
            // 克隆历史记录
            foreach (var historyBoard in this._boardHistory)
            {
                newBoard._boardHistory.Add(CloneBoardState(historyBoard));
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
            // 克隆历史记录
            foreach (var historyBoard in this._boardHistory)
            {
                newBoard._boardHistory.Add(CloneBoardState(historyBoard));
            }
            return newBoard;
        }
        private bool IsOnBoard(Point point)
        {
            return point.Row <= Size && point.Row > 0 &&
                   point.Col <= Size && point.Col > 0;
        }
        public void SetState(Point point, Player color)
        {
            if (color == Player.Black) _board[point.Row, point.Col] = PointState.black;
            else _board[point.Row, point.Col] = PointState.white;
        }
        public void ClearState(Point point)
        {
            _board[point.Row, point.Col] = PointState.None;
        }
        public static List<Point> GetNeighbors(Point point)
        {
            return NEIGHBORS_CACHE[point];
        }

        public static List<Point> GetDiagonals(Point point)
        {
            return DIAGONALS_CACHE[point];
        }

        public int GetLiberty(Point point)
        {
            int liberty = FindGroup(point).liberties;
            return liberty;
        }

        public (HashSet<Point> group, int liberties) FindGroup(Point startPoint)
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
                    _currentHash ^= _zobrist.GetHash(point.Row, point.Col, (int)_board[point.Row, point.Col]);
                }
                _board[point.Row, point.Col] = PointState.None;
            }
        }
        public List<Point> GetEmptyPoints()
        {
            var points = new List<Point>();
            for (int row = 1; row <= Size; ++row)
            {
                for (int col = 1; col <= Size; ++col)
                {
                    var point = new Point(row, col);
                    if (GetPointState(point) == PointState.None) points.Add(point);
                }
            }
            return points;
        }

        public Dictionary<Point, List<Point>> InitializeNeighbors()
        {
            var neighborsDic = new Dictionary<Point, List<Point>>();
            for (int row = 1; row <= Size; ++row)
            {
                for (int col = 1; col <= Size; ++col)
                {
                    var point = new Point(row, col);
                    neighborsDic.Add(point, new List<Point>
                    {
                        new Point(row - 1, col),
                        new Point(row + 1, col),
                        new Point(row, col - 1),
                        new Point(row, col + 1)
                    }.Where(p => IsOnBoard(p)).ToList());
                }
            }
            return neighborsDic;
        }
        public Dictionary<Point, List<Point>> InitializeDiagonals()
        {
            var digonalsDic = new Dictionary<Point, List<Point>>();
            for (int row = 1; row <= Size; ++row)
            {
                for (int col = 1; col <= Size; ++col)
                {
                    var point = new Point(row, col);
                    digonalsDic.Add(point, new List<Point>
                    {
                        new Point(row + 1, col + 1),
                        new Point(row - 1, col - 1),
                        new Point(row + 1, col - 1),
                        new Point(row - 1, col + 1)
                    }.Where(p => IsOnBoard(p)).ToList());
                }
            }
            return digonalsDic;
        }
        public static PointState PlayerToPointState(Player player)
        {
            return player == Player.Black ? PointState.black : PointState.white;
        }

        /// <summary>
        /// 记录当前棋盘状态到历史
        /// </summary>
        public void RecordBoardHistory()
        {
            _boardHistory.Add(CloneBoardState(_board));

            // 只保留最近 MAX_HISTORY_LENGTH 步
            if (_boardHistory.Count > MAX_HISTORY_LENGTH)
            {
                _boardHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 克隆棋盘状态
        /// </summary>
        private PointState[,] CloneBoardState(PointState[,] board)
        {
            var cloned = new PointState[Size + 1, Size + 1];
            Array.Copy(board, cloned, board.Length);
            return cloned;
        }

        /// <summary>
        /// 获取历史棋盘状态
        /// </summary>
        /// <param name="steps">需要的历史步数</param>
        /// <returns>历史棋盘状态列表，从最新到最旧</returns>
        public List<PointState[,]> GetBoardHistory(int steps)
        {
            var result = new List<PointState[,]>();
            int count = Math.Min(steps, _boardHistory.Count);

            // 从最新到最旧获取历史
            for (int i = _boardHistory.Count - 1; i >= _boardHistory.Count - count; i--)
            {
                result.Add(_boardHistory[i]);
            }

            // 如果历史不足，用空棋盘填充
            while (result.Count < steps)
            {
                result.Add(new PointState[Size + 1, Size + 1]);
            }

            return result;
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
        #region Debug
        public void Print()
        {
            Debug.Print("[棋盘状态]");
            // 外层循环 row（行），内层循环 col（列），按行打印
            for (int row = 1; row <= Size; ++row)
            {
                for(int col = 1; col <= Size; ++col)
                {
                    var state = GetPointState(new Point(row, col));
                    char color = state == PointState.black ? 'b' : (state == PointState.white ? 'w' : '*');
                    Debug.Write(color + " ");
                }
                Debug.WriteLine("");
            }
        }
        public void PrintOnConsole()
        {
            Console.WriteLine("[模拟的棋盘状态]");
            // 外层循环 row（行），内层循环 col（列），按行打印
            for (int row = 1; row <= Size; ++row)
            {
                for (int col = 1; col <= Size; ++col)
                {
                    var state = GetPointState(new Point(row, col));
                    char color = state == PointState.black ? 'b' : (state == PointState.white ? 'w' : '*');
                    Console.Write(color + " ");
                }
                Console.WriteLine("");
            }
        }
        #endregion
    }
}
