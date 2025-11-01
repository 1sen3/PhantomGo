using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Serialization;

namespace PhantomGo.Core.Agents
{
    public class AIPlayer : IPlayerAgent, IDisposable
    {
        public PlayerKnowledge Knowledge { get; set; }
        public Player PlayerColor { get; set; }
        public Dictionary<Player, int> MoveCount { get; set; }

        private readonly Random _random = new Random();
        private readonly int _simulationsPerMove;
        private readonly NeuralNetwork _neuralNet;
        private readonly int _boardSize;

        private Point? _koPoint;
        private Point _lastTryAction;

        // 历史记录：记录每一步后双方的实际棋子数
        private readonly List<Dictionary<Player, int>> _moveCountHistory;

        // 空间位置概率
        private static readonly float[] _basePb = new float[]
        {
            1, 2, 1, 2, 1, 2, 1, 2, 1,
            2, 1, 2, 2, 2, 2, 2, 1, 2,
            1, 2, 3, 3, 3, 3, 3, 2, 1,
            2, 2, 3, 4, 4, 4, 3, 2, 2,
            1, 2, 3, 4, 5, 4, 3, 2, 1,
            2, 2, 3, 4, 4, 4, 3, 2, 2,
            1, 2, 3, 3, 3, 3, 3, 2, 1,
            2, 1, 2, 2, 2, 2, 2, 1, 2,
            1, 2, 1, 2, 1, 2, 1, 2, 1
        };

        public AIPlayer(Player playerColor, int simulationPerMove = 800)
        {
            PlayerColor = playerColor;
            Knowledge = new PlayerKnowledge(playerColor);
            MoveCount = new Dictionary<Player, int>
            {
                { Player.Black, playerColor == Player.Black ? 0 : 1 },
                { Player.White, 0 }
            };
            _simulationsPerMove = simulationPerMove;
            _boardSize = 9;

            _moveCountHistory = new List<Dictionary<Player, int>>
            {
                MoveCount
            };

            // 加载 ONNX 模型
            _neuralNet = new NeuralNetwork("D:\\Project\\ComputerGame\\PhantomGo\\PhantomGo\\PhantomGo.Core\\Assets\\model2.onnx");
        }

        public void MakeMove(Point point)
        {
            var result = Knowledge.MakeMove(point);
            if(result.CapturedPoints.Count > 0)
            {
                OnPointCaptured(result.CapturedPoints.ToList());
            }
        }

        /// <summary>
        /// 走法成功后，清除打劫点标记并更新走子计数
        /// </summary>
        public void OnMoveSuccess()
        {
            MoveCount[PlayerColor]++;
            MoveCount[PlayerColor.GetOpponent()]++; // 对手后面也会走一步
            _lastTryAction = Point.Unlegal(); // 预设为无效点

            // 保存当前 MoveCount 的快照到历史记录
            _moveCountHistory.Add(new Dictionary<Player, int>
            {
                { Player.Black, MoveCount[Player.Black] },
                { Player.White, MoveCount[Player.White] }
            });

            if (_koPoint.HasValue)
            {
                Knowledge.RemoveState(_koPoint.Value); // 移除打劫点的非法状态
                _koPoint = null;
            }

            Console.WriteLine($"[AI] 落子成功，当前己方棋子数：{MoveCount[PlayerColor]}，对手棋子数：{MoveCount[PlayerColor.GetOpponent()]}");
        }

        /// <summary>
        /// 走法失败时，标记该点为非法
        /// </summary>
        public void OnMoveFailed()
        {
            if (_koPoint.HasValue && _lastTryAction.Equals(_koPoint.Value))
            {
                Knowledge.MarkAsKoBlocked(_lastTryAction);
                return;
            }
            Knowledge.MarkAsInferred(_lastTryAction); // 更新 Knowledge
        }
        public void OnPointCaptured(List<Point> capturedPoints)
        {
            var capturePlayer = PlayerColor;
            if (Knowledge.GetMemoryState(capturedPoints[0]) == MemoryPointState.Self) // 如果是自己的子被提了，说明是对方提子，否则就是自己提子
            {
                capturePlayer = PlayerColor.GetOpponent();
            }
            MoveCount[capturePlayer.GetOpponent()] -= capturedPoints.Count; // 被提子方棋子数减少
            _moveCountHistory.Last()[capturePlayer.GetOpponent()] -= capturedPoints.Count; // 更新双方棋子历史记录

            // 当只有1个子被提了的时候，判断这个点是不是打劫点
            if (capturedPoints.Count == 1 && capturePlayer == PlayerColor.GetOpponent())
            {
                if (Knowledge.GetBestGuessBoard(PlayerColor).IsKoish(capturedPoints[0]) != PointState.None)
                {
                    _koPoint = capturedPoints[0];
                }
            }

            Knowledge.OnPointCaptured(capturedPoints);

            string captureColor = capturePlayer.GetOpponent() == Player.Black ? "黑子" : "白子";
            StringBuilder sb = new StringBuilder();
            foreach(var p in capturedPoints)
            {
                sb.Append(p.ToString() + "，");
            }
            Console.WriteLine($"[AI]  {captureColor} 的以下棋子被提：{sb.ToString()} 当前己方棋子数：{MoveCount[PlayerColor]}，对手棋子数：{MoveCount[PlayerColor.GetOpponent()]}");
        }
        /// <summary>
        /// 生成一个走法
        /// </summary>
        public (double, Point) GenerateMove()
        {
            Console.Clear();
            Console.WriteLine($"[AI] 开始生成走法，当前己方棋子数：{MoveCount[PlayerColor]}，对手棋子数：{MoveCount[PlayerColor.GetOpponent()]}");

            var totalSw = Stopwatch.StartNew();

            // 模拟可能的完整棋盘状态，同时返回每个棋盘出现的概率
            List<(GoBoard board, float probability)> simulatedBoards = SimulateOpponentBoard(_simulationsPerMove).OrderByDescending(s => s.Item2).ToList();
            simulatedBoards[0].board.PrintOnConsole();

            if (simulatedBoards.Count == 0)
            {
                // 如果模拟结果为空，则 Pass
                Console.WriteLine($"[AI] 模拟失败，无可用棋盘。决策：Pass。");
                _lastTryAction = Point.Pass();
                return (totalSw.Elapsed.TotalSeconds, Point.Pass());
            }

            // 评估并聚合策略
            int boardSizeSq = _boardSize * _boardSize;
            float[] aggregateScoreBoard = new float[boardSizeSq];

            foreach (var (simBoard, boardProbability) in simulatedBoards)
            {
                // 使用神经网络评估这个模拟的棋盘
                var (policy, value) = _neuralNet.Predict(simBoard, PlayerColor);
                // 聚合策略
                for (int i = 0; i < policy.Length && i < boardSizeSq; i++)
                {
                    aggregateScoreBoard[i] += policy[i];
                }
            }

            // 过滤非法或不希望的走法
            var selfBoardOnly = GetSelfBoard();

            // 自己的位置得分设为零
            var selfStones = GetSelfStonePoints();
            foreach (var selfStone in selfStones)
            {
                aggregateScoreBoard[PointToIndex(selfStone)] = 0;
            }

            // 禁止填眼
            var innerEyes = FindInnerEyes(selfBoardOnly);

            Console.WriteLine($"[眼位检测] 找到 {innerEyes.Count} 个内眼：{string.Join(", ", innerEyes)}");

            foreach (var eye in innerEyes)
            {
                aggregateScoreBoard[PointToIndex(eye)] = 0;
            }

            // 合法性过滤
            GoBoard currentFullBoard = Knowledge.GetBestGuessBoard(PlayerColor);
            for (int i = 0; i < aggregateScoreBoard.Length; i++)
            {
                if (aggregateScoreBoard[i] == 0) continue; // 已经被排除过了

                Point p = IndexToPoint(i);
                var state = Knowledge.GetMemoryState(p);

                if (state != MemoryPointState.Unknown)
                {
                    aggregateScoreBoard[i] = 0;
                }

                // 检查自杀等非法手
                if (!currentFullBoard.IsValidMove(p, PlayerColor))
                {
                    aggregateScoreBoard[i] = 0;
                }
            }

            float maxScore = aggregateScoreBoard.Max(); // 选择分数最高的点

            // 没有合法的走法则 Pass
            if (maxScore <= 0)
            {
                totalSw.Stop();

                Console.WriteLine($"[AI] 在 {totalSw.Elapsed.TotalSeconds:F2}秒内完成了 {simulatedBoards.Count} 次模拟。决策：Pass。");

                _lastTryAction = Point.Pass();
                return (totalSw.Elapsed.TotalSeconds, Point.Pass());
            }

            int flatMaxIdx = Array.IndexOf(aggregateScoreBoard, maxScore);
            Point bestMove = IndexToPoint(flatMaxIdx);

            totalSw.Stop();

            Console.WriteLine($"[AI] 在 {totalSw.Elapsed.TotalSeconds:F2}秒内完成了 {simulatedBoards.Count} 次模拟。扁平索引：{flatMaxIdx} 坐标:({bestMove.Row}, {bestMove.Col}) 棋盘点：{bestMove}，分数：{maxScore}");

            _lastTryAction = bestMove;
            return (totalSw.Elapsed.TotalSeconds, bestMove);
        }

        /// <summary>
        /// 模拟对手可能的棋盘布局，返回 (棋盘, 出现概率) 的列表
        /// </summary>
        private List<(GoBoard, float)> SimulateOpponentBoard(int numSimulations)
        {
            const float INFERRED_STONE_WEIGHT = 2500; // 推测为对手棋子的权重
            const float INFERRED_NEIGHBOR_WEIGHT = 100; // 推测为对手棋子邻居的权重
            const float INFERRED_DIAGONAL_WEIGHT = 50; // 推测为对手棋子对角线的权重
            const float NN_WEIGHT = 500; // 神经网络预测的权重

            var simulatedBoards = new List<(GoBoard, float)>();

            GoBoard selfBoardOnly = GetSelfBoard();
            int numOppStones = MoveCount[PlayerColor.GetOpponent()];
            var selfStonePoints = GetSelfStonePoints();

            // 计算对手棋子总数上限
            var innerEyes = FindInnerEyes(selfBoardOnly);
            int numOppStoneUpperLimit = (_boardSize * _boardSize) - selfStonePoints.Count - innerEyes.Count;
            if (numOppStones > numOppStoneUpperLimit)
            {
                numOppStones = numOppStoneUpperLimit;
            }
            // 移除提前返回，让 numOppStones = 0 时也进行模拟
            // 这样黑子第一手会模拟"白子可能下在哪里"，然后基于这些可能性做决策
            // if (numOppStones <= 0)
            // {
            //     simulatedBoards.Add((selfBoardOnly.Clone(), 1.0f));
            //     return simulatedBoards;
            // }

            float[] pb = (float[])_basePb.Clone();

            // 使用神经网络预测对手落子概率
            var opponentBoard = ConstructOpponentViewBoard();
            var (nnPolicy, _) = _neuralNet.Predict(opponentBoard, PlayerColor.GetOpponent());
            for (int i = 0; i < nnPolicy.Length && i < pb.Length; i++)
            {
                pb[i] += nnPolicy[i] * NN_WEIGHT;
            }

            var opponentPoints = GetInferredOpponentPoints();
            foreach (var point in opponentPoints)
            {
                pb[PointToIndex(point)] += INFERRED_STONE_WEIGHT;
                if (opponentPoints.Count > 5) // 前期抢占重要点，认为对方不会下得太集中
                {
                    foreach (var neighbor in GoBoard.GetNeighbors(point))
                    {
                        pb[PointToIndex(neighbor)] += INFERRED_NEIGHBOR_WEIGHT;
                    }
                    foreach (var diagonal in GoBoard.GetDiagonals(point))
                    {
                        pb[PointToIndex(diagonal)] += INFERRED_DIAGONAL_WEIGHT;
                    }
                }
            }

            // 从概率分布中移除己方棋子和真眼
            foreach (var selfStone in selfStonePoints)
            {
                pb[PointToIndex(selfStone)] = 0;
            }
            foreach (var eye in innerEyes)
            {
                pb[PointToIndex(eye)] = 0;
            }

            float pbSum = pb.Sum();
            if (pbSum == 0) // 没有地方可以模拟
            {
                simulatedBoards.Add((selfBoardOnly.Clone(), 1.0f));
                return simulatedBoards;
            }

            // [debug] 打印 pb
            for(int row = 1;row <= 9;++row)
            {
                for(int col = 1;col <= 9;++col)
                {
                    int idx = PointToIndex(new Point(row, col));
                    Debug.Write($"{pb[idx]} ");
                }
                Debug.WriteLine("");
            }

            // 归一化
            for (int i = 0; i < pb.Length; i++) pb[i] /= pbSum;

            Player opponent = PlayerColor.GetOpponent();

            // 5. 开始模拟
            for (int t = 0; t < numSimulations; t++)
            {
                float[] tmpPb = (float[])pb.Clone();
                GoBoard simBoard = selfBoardOnly.Clone();
                bool simulationSuccess = true;
                float boardProbability = 1.0f; // 初始化棋盘出现概率

                // 6. 模拟对手的前 N-1 颗棋子
                for (int i = 0; i < numOppStones - 1; i++)
                {
                    bool movePlaced = false;
                    for (int ntry = 0; ntry < 5; ntry++) // 尝试 5 次
                    {
                        int flatIdx = ChooseRandomIndex(tmpPb);
                        if (flatIdx == -1) break; // 概率总和为 0

                        Point actionOpp = IndexToPoint(flatIdx);

                        // 检查走法是否合法
                        if (!simBoard.IsValidMove(actionOpp, opponent))
                        {
                            continue;
                        }

                        // 跳过所有产生提子的步骤
                        var undoInfo = simBoard.PlaceStoneForSimulation(actionOpp, opponent);
                        if (undoInfo == null || undoInfo.Value.CapturedPoints.Count > 0)
                        {
                            if (undoInfo != null)
                            {
                                simBoard.UndoMove(undoInfo.Value, opponent);
                            }
                            continue;
                        }

                        // 累乘这次选择的概率
                        boardProbability *= tmpPb[flatIdx] * 10;

                        tmpPb[flatIdx] = 0; // 不在同一点下棋

                        float tmpSum = tmpPb.Sum();
                        if (tmpSum == 0) break;
                        // 重新归一化
                        for (int k = 0; k < tmpPb.Length; k++) tmpPb[k] /= tmpSum;

                        movePlaced = true;
                        break;
                    }
                    if (!movePlaced)
                    {
                        simulationSuccess = false; // 无法放置足够的棋子
                        break;
                    }
                }
                if (!simulationSuccess) continue;

                // 7. 模拟对手的最后 1 颗棋子
                for (int q = 0; q < 10; q++) // 尝试 10 次
                {
                    int flatIdx = ChooseRandomIndex(tmpPb);
                    if (flatIdx == -1) break;

                    Point actionOpp = IndexToPoint(flatIdx);

                    if (!simBoard.IsValidMove(actionOpp, opponent))
                    {
                        continue;
                    }

                    var undoInfo = simBoard.PlaceStoneForSimulation(actionOpp, opponent);
                    if (undoInfo == null || undoInfo.Value.CapturedPoints.Count > 0)
                    {
                        if (undoInfo != null)
                        {
                            simBoard.UndoMove(undoInfo.Value, opponent);
                        }
                        continue;
                    }

                    // 累乘最后一颗棋子的概率
                    boardProbability *= tmpPb[flatIdx] * 10;

                    // 模拟成功
                    simulatedBoards.Add((simBoard, boardProbability));
                    break;
                }
            }

            // 如果所有模拟都失败了，至少返回一个基于当前知识的评估
            if (simulatedBoards.Count == 0)
            {
                simulatedBoards.Add((selfBoardOnly.Clone(), 1.0f));
            }

            return simulatedBoards;
        }

        /// <summary>
        /// 找到内部的、且不可能被对手占据的气
        /// </summary>
        private List<Point> FindInnerEyes(GoBoard selfBoard)
        {
            var innerEyes = new List<Point>();
            for (int row = 1; row <= _boardSize; row++)
            {
                for (int col = 1; col <= _boardSize; col++)
                {
                    var p = new Point(row, col);

                    if (!PlayerColor.CompareToPointState(selfBoard.IsEyeish(p)))
                    {
                        continue;
                    }

                    // 检查对角线，防止假眼
                    var diagonals = new[]
                    {
                        new Point(p.Row - 1, p.Col - 1), new Point(p.Row - 1, p.Col + 1),
                        new Point(p.Row + 1, p.Col + 1), new Point(p.Row + 1, p.Col - 1)
                    };

                    int faults = 0; // 对角线上的缺陷数
                    bool isSide = false;
                    foreach (var diagonal in diagonals)
                    {
                        if (diagonal.Row >= 1 && diagonal.Row <= _boardSize && diagonal.Col >= 1 && diagonal.Col <= _boardSize)
                        {
                            if (!PlayerColor.CompareToPointState(selfBoard.GetPointState(diagonal)) && !PlayerColor.CompareToPointState(selfBoard.IsKoish(diagonal)))
                            {
                                faults++;
                            }
                        }
                        else
                        {
                            isSide = true;
                        }
                    }

                    // 如果对角线缺陷太多，则这个眼是假眼
                    // 在中心位置缺陷 >= 2 是假眼
                    // 在边上只要有缺陷就是假眼
                    if (faults >= 2 || (isSide && faults > 0))
                    {
                        continue;
                    }

                    // 通过所有检查，这是一个"真眼"
                    innerEyes.Add(p);
                }
            }
            return innerEyes;
        }

        #region 辅助方法
        /// <summary>
        /// 找到与指定点相连的我方棋子
        /// </summary>
        private void FindConnectSelfGroup(Point start, HashSet<Point> visited)
        {
            if (visited.Contains(start)) return;
            if (Knowledge.GetMemoryState(start) != MemoryPointState.Self) return;
            visited.Add(start);
            foreach(var neighbor in GoBoard.GetNeighbors(start))
            {
                FindConnectSelfGroup(neighbor, visited);
            }
        }

        /// <summary>
        /// 模拟对手看到的棋盘
        /// </summary>
        private GoBoard ConstructOpponentViewBoard()
        {
            var opponentBoard = new GoBoard();

            // 我们推测是对方棋子的点
            var inferredOpponentPoints = GetInferredOpponentPoints();
            foreach (var point in inferredOpponentPoints)
            {
                opponentBoard.PlaceStone(point, PlayerColor.GetOpponent());
            }

            // 计算每个己方棋子被对手感知的概率
            var selfStonePoints = GetSelfStonePoints();
            var visibilityScores = new Dictionary<Point, float>();

            foreach (var selfStone in selfStonePoints)
            {
                float score = CalculateSelfStoneVisibilityProbability(selfStone, inferredOpponentPoints);
                visibilityScores[selfStone] = score;
            }

            // 根据棋子数量确定需要保留多少己方棋子
            // 对手视角：根据历史记录确定当对手有 opponentCount 颗棋子时，我方应该有多少棋子
            int opponentCount = inferredOpponentPoints.Count; // 已推测出的对手棋子数
            int selfCount = selfStonePoints.Count; // 当前棋盘上我的棋子总数
            int targetSelfStonesVisible = 0;

            // 从历史记录中查找：当对手有 opponentCount 颗棋子时，我方有多少棋子
            for (int i = _moveCountHistory.Count - 1; i >= 0; i--)
            {
                if (_moveCountHistory[i][PlayerColor.GetOpponent()] == opponentCount)
                {
                    targetSelfStonesVisible = _moveCountHistory[i][PlayerColor];
                    Console.WriteLine($"[对手视角] 从历史记录找到：对手 {opponentCount} 子时，我方有 {targetSelfStonesVisible} 子");
                    break;
                }
            }

            // 按照可见性概率排序，选择最可能被对手感知的棋子
            var sortedByVisibility = visibilityScores.OrderByDescending(kv => kv.Value).ToList();

            int stonesToKeep = Math.Min(targetSelfStonesVisible, sortedByVisibility.Count);
            for (int i = 0; i < stonesToKeep; i++)
            {
                // 对于对手而言，我的棋子是 InferredOpponent
                opponentBoard.SetState(sortedByVisibility[i].Key, PlayerColor);
                opponentBoard.RecordBoardHistory();
            }

            return opponentBoard;
        }

        /// <summary>
        /// 计算己方棋子被对手感知的概率（基于距离）
        /// </summary>
        private float CalculateSelfStoneVisibilityProbability(Point selfStone, List<Point> opponentStones)
        {
            if (opponentStones.Count == 0)
            {
                return 0.2f; // 没有已知对手棋子时，默认较低概率
            }

            float maxProb = 0f;

            foreach (var oppStone in opponentStones)
            {
                int distance = Math.Abs(selfStone.Row - oppStone.Row) + Math.Abs(selfStone.Col - oppStone.Col);

                float prob = distance switch
                {
                    0 => 1.0f,  // 不可能，但防御性编程
                    1 => 1.0f,  // 直接相邻，100% 被感知
                    2 => 0.8f,  // 距离2，80% 概率
                    3 => 0.5f,  // 距离3，50% 概率
                    _ => 0.2f   // 更远，20% 概率
                };

                maxProb = Math.Max(maxProb, prob);
            }

            return maxProb;
        }

        /// <summary>
        /// 获取一个只包含自己棋子的 GoBoard
        /// </summary>
        private GoBoard GetSelfBoard()
        {
            GoBoard board = new GoBoard();
            for (int row = 1; row <= _boardSize; row++)
            {
                for (int col = 1; col <= _boardSize; col++)
                {
                    var p = new Point(row, col);
                    if (Knowledge.GetMemoryState(p) == MemoryPointState.Self)
                    {
                        board.SetState(p, PlayerColor);
                    }
                }
            }
            return board;
        }

        /// <summary>
        /// 获取自己所有棋子的位置
        /// </summary>
        private List<Point> GetSelfStonePoints()
        {
            var points = new List<Point>();
            for (int row = 1; row <= _boardSize; row++)
            {
                for (int col = 1; col <= _boardSize; col++)
                {
                    var p = new Point(row, col);
                    if (Knowledge.GetMemoryState(p) == MemoryPointState.Self)
                    {
                        points.Add(p);
                    }
                }
            }
            return points;
        }
        private List<Point> GetInferredOpponentPoints()
        {
            var points = new List<Point>();
            for (int row = 1; row <= _boardSize; row++)
            {
                for (int col = 1; col <= _boardSize; col++)
                {
                    var p = new Point(row, col);
                    if (Knowledge.GetMemoryState(p) == MemoryPointState.InferredOpponent)
                    {
                        points.Add(p);
                    }
                }
            }
            return points;
        }

        /// <summary>
        /// 根据归一化的概率分布随机选择一个索引
        /// </summary>
        private int ChooseRandomIndex(float[] probabilities)
        {
            double r = _random.NextDouble();
            double cumulative = 0;
            for (int i = 0; i < probabilities.Length; i++)
            {
                if (probabilities[i] > 0)
                {
                    cumulative += probabilities[i];
                    if (r < cumulative)
                    {
                        return i;
                    }
                }
            }
            //  fallback
            for (int i = 0; i < probabilities.Length; i++)
            {
                if (probabilities[i] > 0) return i;
            }
            return -1; // 如果概率总和为0
        }

        /// <summary>
        /// 将 (row, col) 坐标 (1-based) 转换为扁平索引 (0-based)
        /// </summary>
        private int PointToIndex(Point p)
        {
            return (p.Row - 1) * _boardSize + (p.Col - 1);
        }

        /// <summary>
        /// 将扁平索引 (0-based) 转换为 (row, col) 坐标 (1-based)
        /// </summary>
        private Point IndexToPoint(int index)
        {
            int row = (index / _boardSize) + 1;
            int col = (index % _boardSize) + 1;
            return new Point(row, col);
        }

        #endregion

        public void Dispose() => _neuralNet?.Dispose();
    }
}
