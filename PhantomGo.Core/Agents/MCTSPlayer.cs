using PhantomGo.Core.Agents;
using PhantomGo.Core.Helper;
using PhantomGo.Core.Helpers;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PhantomGo.Core.Agents
{
    public class MCTSPlayer : IPlayerAgent
    {
        public PlayerKnowledge Knowledge { get; }
        public Player PlayerColor { get; }
        public int MoveCount { get; set; }

        private readonly Random _random = new Random();
        private readonly int _simulationsPerMove;

        private readonly Evaluator _evaluator;

        // Debug info
        private int selectCount;
        private int expandCount;
        private int simulateCount;

        public MCTSPlayer(int boardSize, Player playerColor, int simulationPerMove = 100)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            PlayerColor = playerColor;
            MoveCount = 0;
            _simulationsPerMove = simulationPerMove;
            _evaluator = new Evaluator();
        }
        public void OnMoveSuccess() => MoveCount++;

        public Point GenerateMove()
        {
            var josekiMove = JosekiHelper.GetJosekiMove(MoveCount, Knowledge, PlayerColor);
            if (josekiMove.HasValue) return josekiMove.Value;

            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var root = new MCTSNode(Knowledge, PlayerColor);

            long selectTicks = 0;
            long expandTicks = 0;
            long simulateTicks = 0;
            long backpropTicks = 0;
            selectCount = 0; expandCount = 0; simulateCount = 0;

            for (int i = 0; i < _simulationsPerMove; ++i)
            {
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                MCTSNode node = Select(root);
                selectTicks += sw1.ElapsedTicks;

                if (!node.IsTerminal)
                {
                    var sw2 = System.Diagnostics.Stopwatch.StartNew();
                    node = Expand(node);
                    expandTicks += sw2.ElapsedTicks;
                }
                
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                Player winner = Simulate(node);
                simulateTicks += sw3.ElapsedTicks;

                var sw4 = System.Diagnostics.Stopwatch.StartNew();
                Backpropogate(node, winner);
                backpropTicks += sw4.ElapsedTicks;
            }

            totalSw.Stop();

            double selectMs = selectTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double expandMs = expandTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double simulateMs = simulateTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double backpropMs = backpropTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            System.Diagnostics.Debug.WriteLine($@"
                性能分析 ({_simulationsPerMove} 次模拟):
                - 总耗时: {totalSw.Elapsed.TotalSeconds:F2}秒
                - Select:    {selectMs:F0}ms ({selectMs / totalSw.ElapsedMilliseconds * 100:F1}%), {selectCount}次
                - Expand:    {expandMs:F0}ms ({expandMs / totalSw.ElapsedMilliseconds * 100:F1}%), {expandCount} 次
                - Simulate:  {simulateMs:F0}ms ({simulateMs / totalSw.ElapsedMilliseconds * 100:F1}%), {simulateCount}次
                - Backprop:  {backpropMs:F0}ms ({backpropMs / totalSw.ElapsedMilliseconds * 100:F1}%)
                ");

            var bestMoveNode = root.Children
                .OrderByDescending(c => (double)c.Wins / c.Visits)
                .FirstOrDefault();

            if (bestMoveNode != null)
            {
                double winRate = bestMoveNode.Visits > 0 ? (double)bestMoveNode.Wins / bestMoveNode.Visits : 0;
                System.Diagnostics.Debug.WriteLine($"[IS-MCTS] 决策: {bestMoveNode.Move}, 胜率: {winRate:P2}, 访问: {bestMoveNode.Visits}");
                return bestMoveNode.Move;
            }

            return Point.Pass();
        }


        #region MCTS 核心方法
        private MCTSNode Select(MCTSNode node)
        {
            selectCount++;
            while(node.IsFullyExpanded && !node.IsTerminal)
            {
                node = node.SelectBestChild();
            }
            return node;
        }

        private MCTSNode Expand(MCTSNode node)
        {
            expandCount++;

            Point move = node.PopUntriedMove();

            PlayerKnowledge newKnowledge = node.Knowledge.Clone();
            if(node.PlayerToMove == PlayerColor)
            {
                newKnowledge.AddOwnState(move);
            } else
            {
                newKnowledge.MarkAsInferred(move);
            }

            var childrenNode = new MCTSNode(newKnowledge, node.PlayerToMove.GetOpponent(), node, move);
            node.Children.Add(childrenNode);
            return childrenNode;
        }
        private Player Simulate(MCTSNode node)
        {
            simulateCount++;

            GoBoard simBoard = node.Knowledge.GetBestGuessBoard(node.PlayerToMove);
            Player currentPlayer = node.PlayerToMove;
            int consecutivePasses = 0;
            int maxMoves = 50;
            for(int i = 0;i < maxMoves;++i)
            {
                var candidates = GetCandidateMoves(simBoard, currentPlayer);

                if(candidates.Count == 0)
                {
                    consecutivePasses++;
                    if (consecutivePasses >= 2) break;
                    currentPlayer = currentPlayer.GetOpponent();
                    continue;
                }
                Point selectedMove;

                if(_random.NextDouble() < 0.8 && candidates.Count > 3)
                {
                    selectedMove = candidates.Take(3).OrderBy(_ => _random.Next()).First();
                } else
                {
                    selectedMove = candidates[_random.Next(candidates.Count)];
                }

                var result = simBoard.PlaceStone(selectedMove, currentPlayer);
                if(!result.IsSuccess)
                {
                    consecutivePasses++;
                    if (consecutivePasses >= 2) break;
                } else
                {
                    consecutivePasses = 0;
                }
                currentPlayer = currentPlayer.GetOpponent();
            }
            return new ScoreCalculator(simBoard).CalculateScores().Winner;
        }


        private void Backpropogate(MCTSNode node, Player winner)
        {
            MCTSNode tempNode = node;
            while(tempNode != null)
            {
                tempNode.Visits++;

                if (tempNode.Parent != null && tempNode.Parent.PlayerToMove == winner)
                {
                    tempNode.Wins++;
                }

                tempNode = tempNode.Parent;
            }
        }
        #endregion

        #region MCTS 节点类
        internal class MCTSNode
        {
            public PlayerKnowledge Knowledge { get; }
            public Player PlayerToMove { get; }
            public Point Move { get; }
            public MCTSNode Parent { get; }
            public List<MCTSNode> Children { get; }
            public int Visits { get; set; }
            public int Wins { get; set; }
            private readonly List<Point> _untriedMoves;

            public bool IsTerminal => _untriedMoves.Count == 0 && !Children.Any();
            public bool IsFullyExpanded => _untriedMoves.Count == 0;

            public MCTSNode(PlayerKnowledge playerKnowledge, Player playerToMove, MCTSNode parent = null, Point move = default)
            {
                Knowledge = playerKnowledge;
                PlayerToMove = playerToMove;
                Parent = parent;
                Move = move;
                Children = new List<MCTSNode>();

                _untriedMoves = GetPossibleMoves(Knowledge);
                _untriedMoves.Shuffle();
            }

            public Point PopUntriedMove()
            {
                var move = _untriedMoves.Last();
                _untriedMoves.RemoveAt(_untriedMoves.Count() - 1);
                return move;
            }

            public MCTSNode SelectBestChild()
            {
                return Children.OrderByDescending(c => (double)c.Wins / c.Visits + 1.41 * Math.Sqrt(Math.Log(Visits) / c.Visits)).First();
            }

            private List<Point> GetPossibleMoves(PlayerKnowledge playerKnowledge)
            {
                var allUnknown = new List<Point>();
                for (int x = 1; x <= playerKnowledge.BoardSize; ++x)
                {
                    for (int y = 1; y <= playerKnowledge.BoardSize; ++y)
                    {
                        var point = new Point(x, y);
                        if (playerKnowledge.GetMemoryState(point) == MemoryPointState.Unknown)
                        {
                            allUnknown.Add(point);
                        }
                    }
                }

                if (allUnknown.Count > 15)
                {
                    var board = playerKnowledge.GetBestGuessBoard(PlayerToMove);
                    var scoredMoves = new List<(Point point, double score)>();

                    foreach (var move in allUnknown)
                    {
                        if (!board.IsValidMove(move, PlayerToMove)) continue;
                        double score = QuickEvaluate(board, move, PlayerToMove);
                        scoredMoves.Add((move, score));
                    }

                    return scoredMoves
                        .OrderByDescending(m => m.score)
                        .Take(15)  // 只保留前 15 个
                        .Select(m => m.point)
                        .ToList();
                }

                return allUnknown;
            }

            public static List<Point> GetValidMoves(GoBoard board, Player player)
            {
                var moves = new List<Point>();
                for (int x = 1; x <= board.Size; x++)
                {
                    for (int y = 1; y <= board.Size; y++)
                    {
                        var point = new Point(x, y);
                        if (board.IsValidMove(point, player)) moves.Add(point);
                    }
                }
                return moves;
            }

            private double QuickEvaluate(GoBoard board, Point point, Player player)
            {
                double score = 0;
                var myColor = GoBoard.PlayerToPointState(player);
                var oppColor = GoBoard.PlayerToPointState(player.GetOpponent());

                // 1. 靠近己方棋子
                foreach (var n in board.GetNeighbors(point))
                {
                    if (board.GetPointState(n) == myColor)
                    {
                        score += 3.0;
                        break;
                    }
                }
                foreach (var d in board.GetDiagonals(point))
                {
                    if (board.GetPointState(d) == myColor)
                    {
                        score += 1.5;
                        break;
                    }
                }

                // 2. 提子
                foreach (var n in board.GetNeighbors(point))
                {
                    if (board.GetPointState(n) == oppColor)
                    {
                        if (board.GetLiberty(n) == 1) score += 5.0;
                        else if (board.GetLiberty(n) == 2) score += 1.0;
                    }
                }

                // 3. 逃跑
                foreach (var n in board.GetNeighbors(point))
                {
                    if (board.GetPointState(n) == myColor && board.GetLiberty(n) <= 2)
                    {
                        score += 3.0;
                    }
                }

                return score;
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 启发式候选着手生成
        /// </summary> 
        private List<Point> GetCandidateMoves(GoBoard board, Player player)
        {
            var currentPlayerColor = GoBoard.PlayerToPointState(player);
            var moves = new List<(Point point, Double score)>();

            for(int x = 1;x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    var point = new Point(x, y);
                    if (!board.IsValidMove(point, player)) continue;
                    double score = 0;

                    var opponent = GoBoard.PlayerToPointState(player.GetOpponent());

                    // 使用原始棋盘评估
                    // 1.靠近已有棋子
                    bool nearStone = false;
                    foreach (var neighbor in board.GetNeighbors(point))
                    {
                        if (board.GetPointState(neighbor) == currentPlayerColor)
                        {
                            nearStone = true;
                            score += 3.0;
                            break;
                        }
                    }

                    if (!nearStone)
                    {
                        bool nearDiagonoal = false;
                        foreach (var diagonal in board.GetDiagonals(point))
                        {
                            if (board.GetPointState(diagonal) == currentPlayerColor)
                            {
                                nearDiagonoal = true;
                                score += 1.0;
                                break;
                            }
                        }

                        if (!nearDiagonoal) score -= 2.0;
                    }

                    // 2.避免填自己的眼
                    var eyeColor = IsEyeish(board, point);
                    if (eyeColor == currentPlayerColor)
                    {
                        score -= 10.0;
                    }

                    // 3.逃跑
                    foreach (var neighbor in board.GetNeighbors(point))
                    {
                        if (board.GetPointState(neighbor) == currentPlayerColor)
                        {
                            var liberty = board.GetLiberty(neighbor);
                            if (liberty <= 2) score += 3.0;
                        }
                    }

                    // 使用模拟落子评估
                    var undoInfo = board.PlaceStoneForSimulation(point, player);
                    if (!undoInfo.HasValue) continue;

                    // 1.提子
                    if(undoInfo.Value.CapturedPoints.Count > 0)
                    {
                        score += 6.0 + (undoInfo.Value.CapturedPoints.Count - 1) * 2.0;
                    }

                    // 2.做眼
                    foreach(var neighbor in board.GetNeighbors(point))
                    {
                        if(IsEyeish(board, neighbor) == currentPlayerColor)
                        {
                            score += 4.5;
                        }
                    }

                    // 3.叫吃
                    foreach(var neighbor in board.GetNeighbors(point))
                    {
                        if(board.GetPointState(neighbor) == opponent && board.GetLiberty(neighbor) == 1)
                        {
                            score += 4.0;
                        }
                    }

                    board.UndoMove(undoInfo.Value, player);
                    moves.Add((point, score));
                }
            }
            // 返回按分数排序的着手
            return moves.OrderByDescending(m => m.score).Select(m => m.point).ToList();
        }

        private PointState IsKoish(GoBoard board, Point point)
        {
            if(board.GetPointState(point) != PointState.None) return PointState.None;
            var neighborColors = board.GetNeighbors(point).Select(n => board.GetPointState(n)).ToHashSet();
            if(neighborColors.Count == 1 && !neighborColors.Contains(PointState.None)) {
                return neighborColors.First();
            } else
            {
                return PointState.None;
            }
        }
        private PointState IsEyeish(GoBoard board, Point point)
        {
            if (point == Point.Pass()) return PointState.None;
            var color = IsKoish(board, point);
            if (color == PointState.None) return PointState.None;
            var colorSet = new HashSet<PointState> { color, PointState.None };
            int diagonalFaults = 0;
            var diagonals = board.GetDiagonals(point);
            if(diagonals.Count < 4)
            {
                diagonalFaults += 1;
            }
            foreach(var diagonal in diagonals)
            {
                if(!colorSet.Contains(board.GetPointState(diagonal))) {
                    diagonalFaults += 1;
                }
            }
            if (diagonalFaults > 1) return PointState.None;
            else return color;
        }
        #endregion
    }
}