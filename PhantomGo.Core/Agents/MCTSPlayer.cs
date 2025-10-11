using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PhantomGo.Core.Agents
{
    public class MCTSPlayer : IPlayerAgent, IDisposable
    {
        public PlayerKnowledge Knowledge { get; }
        public Player PlayerColor { get; }
        public int MoveCount { get; set; }

        private readonly Random _random = new Random();
        private readonly int _simulationsPerMove;
        private readonly NeuralNetwork _neuralNet;

        public MCTSPlayer(int boardSize, Player playerColor, int simulationPerMove = 800)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            PlayerColor = playerColor;
            MoveCount = 0;
            _simulationsPerMove = simulationPerMove;

            // 加载 ONNX 模型
            _neuralNet = new NeuralNetwork("D:\\Project\\ComputerGame\\PhantomGo\\PhantomGo\\PhantomGo.Core\\Assets\\model.onnx");
        }

        public void OnMoveSuccess() => MoveCount++;

        public Point GenerateMove()
        {
            var totalSw = Stopwatch.StartNew();
            var root = new MCTSNode(Knowledge, PlayerColor, this.MoveCount);

            // 在根节点进行第一次扩展和评估
            ExpandAndEvaluate(root);

            for (int i = 0; i < _simulationsPerMove; ++i)
            {
                MCTSNode node = Select(root);
                ExpandAndEvaluate(node);
            }

            totalSw.Stop();
            Debug.WriteLine($"[NN-MCTS] 在 {totalSw.Elapsed.TotalSeconds:F2}秒内完成了 {_simulationsPerMove} 次模拟。");

            // 决策：选择访问次数最多的节点（这比选择胜率最高的更稳健）
            if (root.Children.Count == 0) return Point.Pass();
            var bestMoveNode = root.Children.OrderByDescending(c => c.Visits).FirstOrDefault();

            return bestMoveNode?.Move ?? Point.Pass();
        }

        private MCTSNode Select(MCTSNode node)
        {
            // 只要节点不是叶子节点（即已经被扩展过），就继续往下选择
            while (!node.IsLeaf())
            {
                node = node.SelectBestChild();
            }
            return node;
        }

        private void ExpandAndEvaluate(MCTSNode node)
        {
            // 1. 获取一个用于评估的棋盘状态
            var boardForEval = node.Knowledge.GetBestGuessBoard(node.PlayerToMove);

            // 如果游戏已经结束，则直接根据结果进行反向传播
            if (boardForEval.GameState == GameState.Ended)
            {
                ScoreCalculator scCalculator = new ScoreCalculator(boardForEval);
                var score = scCalculator.CalculateScores();
                float realValue = (score.Winner == node.PlayerToMove) ? 1.0f : -1.0f;
                Backpropagate(node, realValue);
                return;
            }

            // 2. 使用神经网络进行一次评估，同时获得“棋感”（策略）和“大局观”（价值）
            var (policy, value) = _neuralNet.Predict(node.Knowledge, node.PlayerToMove);
            
            // 3. 扩展：根据策略网络的建议，创建所有可能的子节点
            var legalMoves = MCTSNode.GetPossibleMoves(node.Knowledge);
            foreach (var move in legalMoves)
            {
                PlayerKnowledge newKnowledge = node.Knowledge.Clone();
                if(node.PlayerToMove == PlayerColor)
                {
                    newKnowledge.AddOwnState(move);
                } else
                {
                    newKnowledge.MarkAsInferred(move);
                }

                    int moveIndex = (move.Y - 1) * 9 + (move.X - 1);
                float priorProbability = policy[moveIndex];

                node.Children.Add(new MCTSNode(newKnowledge, node.PlayerToMove.GetOpponent(), node.MoveCount + 1, node, move, priorProbability));
            }

            // 4. 反向传播神经网络给出的“大局观”价值
            Backpropagate(node, value);
        }

        private void Backpropagate(MCTSNode node, float value)
        {
            MCTSNode tempNode = node;
            while (tempNode != null)
            {
                tempNode.Visits++;
                // 价值对于当前节点玩家是value，对于上一步（对手）则是-value
                tempNode.TotalValue += (tempNode.PlayerToMove != node.PlayerToMove) ? value : -value;
                tempNode = tempNode.Parent;
            }
        }

        public void Dispose() => _neuralNet?.Dispose();

        #region MCTS 节点类 (已重写)
        internal class MCTSNode
        {
            public PlayerKnowledge Knowledge { get; }
            public Player PlayerToMove { get; }
            public Point Move { get; }
            public MCTSNode Parent { get; }
            public List<MCTSNode> Children { get; }
            public int Visits { get; set; }
            public float TotalValue { get; set; }
            public int MoveCount { get; }
            public float Prior { get; } // 存储策略网络给出的先验概率

            public MCTSNode(PlayerKnowledge knowledge, Player playerToMove, int moveCount, MCTSNode parent = null, Point move = default, float prior = 0)
            {
                Knowledge = knowledge;
                PlayerToMove = playerToMove;
                MoveCount = moveCount;
                Parent = parent;
                Move = move;
                Prior = prior;
                Children = new List<MCTSNode>();
            }

            public bool IsLeaf() => Children.Count == 0;

            public MCTSNode SelectBestChild()
            {
                const float c_puct = 1.41f;

                return Children.OrderByDescending(child =>
                {
                    // 如果子节点从未被访问过，返回一个极大的分数
                    if (child.Visits == 0)
                    {
                        // 在PUCT中，通常用先验概率本身作为初始分数
                        return float.PositiveInfinity;
                    }

                    // PUCT公式
                    float Q = child.TotalValue / child.Visits;
                    float U = c_puct * child.Prior * (float)Math.Sqrt(this.Visits) / (1 + child.Visits);
                    return Q + U;

                }).First();
            }

            public static List<Point> GetPossibleMoves(PlayerKnowledge knowledge)
            {
                var moves = new List<Point>();
                for (int x = 1; x <= knowledge.BoardSize; x++)
                {
                    for (int y = 1; y <= knowledge.BoardSize; y++)
                    {
                        var point = new Point(x, y);
                        if (knowledge.GetMemoryState(point) == MemoryPointState.Unknown)
                            moves.Add(point);
                    }
                }
                return moves;
            }
        }
        #endregion
    }
}