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

        public MCTSPlayer(int boardSize, Player playerColor, int simulationPerMove = 1000)
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

            // ✅ 分别计时三个阶段
            long selectTicks = 0;
            long simulateTicks = 0;
            long backpropTicks = 0;

            for (int i = 0; i < _simulationsPerMove; ++i)
            {
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                MCTSNode node = Select(root);
                selectTicks += sw1.ElapsedTicks;

                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                Player winner = Simulate(node);
                simulateTicks += sw2.ElapsedTicks;

                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                Backpropogate(node, winner);
                backpropTicks += sw3.ElapsedTicks;
            }

            totalSw.Stop();

            double selectMs = selectTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double simulateMs = simulateTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double backpropMs = backpropTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            System.Diagnostics.Debug.WriteLine($@"
                性能分析 ({_simulationsPerMove} 次模拟):
                - 总耗时: {totalSw.Elapsed.TotalSeconds:F2}秒
                - Select:    {selectMs:F0}ms ({selectMs / totalSw.ElapsedMilliseconds * 100:F1}%)
                - Simulate:  {simulateMs:F0}ms ({simulateMs / totalSw.ElapsedMilliseconds * 100:F1}%)
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
            while(!node.IsTerminal)
            {
                if (!node.IsFullyExpanded) return Expand(node);
                node = node.SelectBestChild();
            }
            return node;
        }

        private MCTSNode Expand(MCTSNode node)
        {
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
            GoBoard simBoard = node.Knowledge.Determinize(node.PlayerToMove);
            Player currentPlayer = node.PlayerToMove;

            // 预先收集所有空点
            var emptyPoints = new List<Point>();
            for (int x = 1; x <= simBoard.Size; x++)
            {
                for (int y = 1; y <= simBoard.Size; y++)
                {
                    var point = new Point(x, y);
                    if (simBoard.GetPointState(point) == PointState.None)
                        emptyPoints.Add(point);
                }
            }

            int consecutivePassesInSim = 0;
            int maxSteps = Math.Min(40, emptyPoints.Count);  // ✅ 限制模拟深度

            for (int i = 0; i < maxSteps && emptyPoints.Count > 0; ++i)
            {
                Point bestMove = Point.Pass();
                double bestScore = double.MinValue;

                // 遍历所有合法空点，找到评估分数最高的点
                foreach (var move in emptyPoints)
                {
                    if (simBoard.IsValidMove(move, currentPlayer))
                    {
                        GoBoard tmpBoard = simBoard.Clone();
                        tmpBoard.PlaceStone(move, currentPlayer);
                        double score = _evaluator.Evaluate(tmpBoard, currentPlayer);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMove = move;
                        }
                    }
                }

                // 如果没有找到任何可以落子的点，就pass
                if (bestMove.Equals(Point.Pass()))
                {
                    consecutivePassesInSim++;
                    if (consecutivePassesInSim >= 2) break;
                }
                else
                {
                    simBoard.PlaceStone(bestMove, currentPlayer);
                    emptyPoints.Remove(bestMove);
                    consecutivePassesInSim = 0;
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
                var moves = new List<Point>();
                for(int x = 1;x <= playerKnowledge.BoardSize;++x)
                {
                    for(int y = 1;y <= playerKnowledge.BoardSize;++y)
                    {
                        var point = new Point(x, y);
                        if(playerKnowledge.GetMemoryState(point) == MemoryPointState.Unknown)
                        {
                            moves.Add(point);
                        }
                    }
                }
                return moves;
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
        }
        #endregion
    }
}