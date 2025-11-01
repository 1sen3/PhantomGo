using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PhantomGo.Core.Agents
{
    public class AIForTest : IPlayerAgent, IDisposable
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
        public void MakeMove(Point point)
        {
            var result = Knowledge.MakeMove(point);
            if (result.CapturedPoints.Count > 0)
            {
                OnPointCaptured(result.CapturedPoints.ToList());
            }
        }
        public AIForTest(Player playerColor, int simulationPerMove = 800)
        {
            PlayerColor = playerColor;
            Knowledge = new PlayerKnowledge(playerColor);
            MoveCount = new Dictionary<Player, int>
            {
                { Player.Black, 0 },
                { Player.White, 0 }
            };
            _simulationsPerMove = simulationPerMove;
            _boardSize = 9;

            // 加载 ONNX 模型
            _neuralNet = new NeuralNetwork("D:\\Project\\ComputerGame\\PhantomGo\\PhantomGo\\PhantomGo.Core\\Assets\\model1.onnx");
        }

        /// <summary>
        /// 走法成功后，清除"非法走法"标记
        /// </summary>
        public void OnMoveSuccess()
        {
            MoveCount[PlayerColor]++;
            MoveCount[PlayerColor.GetOpponent()]++; // 对手后面也会走一步
            _lastTryAction = Point.Unlegal(); // 预设为无效点

            if(_koPoint.HasValue)
            {
                Knowledge.RemoveState(_koPoint.Value); // 移除打劫点的非法状态
                _koPoint = null;
            }
        }

        /// <summary>
        /// 走法失败时，标记该点为非法
        /// </summary>
        public void OnMoveFailed()
        {
            if(_koPoint.HasValue && _lastTryAction.Equals(_koPoint.Value))
            {
                Knowledge.MarkAsKoBlocked(_lastTryAction);
                return;
            }
            Knowledge.MarkAsInferred(_lastTryAction); // 更新 Knowledge
        }
        public void OnPointCaptured(List<Point> capturedPoints)
        {
            var capturePlayer = PlayerColor;
            if(Knowledge.GetMemoryState(capturedPoints[0]) == MemoryPointState.Self) // 如果是自己的子被提了，说明是对方提子，否则就是自己提子
            {
               capturePlayer = PlayerColor.GetOpponent();
            }
            MoveCount[capturePlayer.GetOpponent()] -= capturedPoints.Count; // 被提子方棋子数减少

            // 当只有1个子被提了的时候，判断这个点是不是打劫点
            if(capturedPoints.Count == 1 && capturePlayer == PlayerColor.GetOpponent())
            {
                if (Knowledge.GetBestGuessBoard(PlayerColor).IsKoish(capturedPoints[0]) != PointState.None)
                {
                    _koPoint = capturedPoints[0];
                }
            }

            Knowledge.OnPointCaptured(capturedPoints);
        }
        /// <summary>
        /// 生成一个走法，使用 "信念状态平均" 策略
        /// </summary>
        public (double, Point) GenerateMove()
        {
            var totalSw = Stopwatch.StartNew();

            // 1. 模拟 N 个可能的完整棋盘状态
            List<PlayerKnowledge> simulatedKnowledges = SimulateOpponentKnowledge(_simulationsPerMove);

            if (simulatedKnowledges.Count == 0)
            {
                // 如果模拟 0 个棋盘（例如无处可下），则 Pass
                Debug.WriteLine($"[AI] 模拟失败，无可用棋盘。决策：Pass。");
                _lastTryAction = Point.Pass();
                return (totalSw.Elapsed.TotalSeconds, Point.Pass());
            }

            // 2. 批量评估并聚合策略
            int boardSizeSq = _boardSize * _boardSize;
            float[] aggregateScoreBoard = new float[boardSizeSq];

            foreach (var simKnowledge in simulatedKnowledges)
            {
                // 使用神经网络评估这个模拟的棋盘
                var (policy, value) = _neuralNet.Predict(simKnowledge, PlayerColor);

                // 只聚合策略 (落子概率)
                for (int i = 0; i < policy.Length && i < boardSizeSq; i++)
                {
                    aggregateScoreBoard[i] += policy[i];
                }
            }

            // 3. 过滤非法或不希望的走法

            // 获取只包含己方棋子的棋盘，用于后续判断
            var selfBoardOnly = GetSelfBoard();

            // 自己的位置得分设为零
            var selfStones = GetSelfStonePoints();
            foreach (var selfStone in selfStones)
            {
                aggregateScoreBoard[PointToIndex(selfStone)] = 0;
            }

            // 禁止填眼
            var innerEyes = FindInnerEyes(selfBoardOnly);
            Debug.WriteLine($"[眼位检测] 找到 {innerEyes.Count} 个内眼：{string.Join(", ", innerEyes)}");
            foreach (var eye in innerEyes)
            {
                aggregateScoreBoard[PointToIndex(eye)] = 0;
            }

            // 合法性过滤
            GoBoard currentFullBoard = Knowledge.GetBestGuessBoard(PlayerColor);
            for (int i = 0; i < aggregateScoreBoard.Length; i++)
            {
                if (aggregateScoreBoard[i] == 0) continue; // 已被之前的过滤排除了

                Point p = IndexToPoint(i);
                var state = Knowledge.GetMemoryState(p);

                if(state != MemoryPointState.Unknown)
                {
                    aggregateScoreBoard[i] = 0;
                }

                // 检查自杀等非法手
                if (!currentFullBoard.IsValidMove(p, PlayerColor))
                {
                    aggregateScoreBoard[i] = 0;
                }
            }

            // 4. 决策：选择分数最高的点
            float maxScore = aggregateScoreBoard.Max();

            // Pass 的情况
            if (maxScore == 0)
            {
                totalSw.Stop();
                Debug.WriteLine($"[AI] 在 {totalSw.Elapsed.TotalSeconds:F2}秒内完成了 {simulatedKnowledges.Count} 次模拟。决策：Pass。");
                _lastTryAction = Point.Pass();
                return (totalSw.Elapsed.TotalSeconds, Point.Pass());
            }

            int flatMaxIdx = Array.IndexOf(aggregateScoreBoard, maxScore);
            Point bestMove = IndexToPoint(flatMaxIdx);

            totalSw.Stop();
            Debug.WriteLine($"[AI] 在 {totalSw.Elapsed.TotalSeconds:F2}秒内完成了 {simulatedKnowledges.Count} 次模拟。决策：{bestMove}，分数：{maxScore}");

            _lastTryAction = bestMove; // 记录本次尝试
            return (totalSw.Elapsed.TotalSeconds, bestMove);
        }

        /// <summary>
        /// 模拟对手可能的棋盘布局
        /// </summary>
        private List<PlayerKnowledge> SimulateOpponentKnowledge(int numSimulations)
        {
            var simulatedKnowledges = new List<PlayerKnowledge>();

            // 1. 获取基础信息
            GoBoard selfBoardOnly = GetSelfBoard();
            int numOppStones = MoveCount[PlayerColor.GetOpponent()];
            var selfStonePoints = GetSelfStonePoints();

            // 2. 计算对手棋子总数上限
            var innerEyes = FindInnerEyes(selfBoardOnly);
            int numOppStoneUpperLimit = (_boardSize * _boardSize) - selfStonePoints.Count - innerEyes.Count;
            if (numOppStones > numOppStoneUpperLimit)
            {
                numOppStones = numOppStoneUpperLimit;
            }
            if (numOppStones <= 0) // 如果对手没棋子了
            {
                simulatedKnowledges.Add(Knowledge.Clone());
                return simulatedKnowledges;
            }

            // 3. 准备基础概率分布 (pb)
            float[] pb = (float[])_basePb.Clone();

            // 4. 从概率分布中移除己方棋子和真眼
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
                simulatedKnowledges.Add(Knowledge.Clone());
                return simulatedKnowledges;
            }

            // 归一化
            for (int i = 0; i < pb.Length; i++) pb[i] /= pbSum;

            Player opponent = PlayerColor.GetOpponent();

            // 5. 开始模拟
            for (int t = 0; t < numSimulations; t++)
            {
                float[] tmpPb = (float[])pb.Clone();
                GoBoard simBoard = selfBoardOnly.Clone();
                PlayerKnowledge simKnowledge = Knowledge.Clone();
                bool simulationSuccess = true;

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
                        var playResult = simBoard.PlaceStone(actionOpp, opponent);
                        if (playResult.CapturedPoints.Count > 0)
                        {
                            continue;
                        }

                        simKnowledge.MarkAsInferred(actionOpp); // 更新 Knowledge
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

                    var playResult = simBoard.PlaceStone(actionOpp, opponent);
                    if (playResult.CapturedPoints.Count > 0)
                    {
                        continue;
                    }

                    // 模拟成功
                    simKnowledge.MarkAsInferred(actionOpp);
                    simulatedKnowledges.Add(simKnowledge);
                    break;
                }
            }

            // 如果所有模拟都失败了，至少返回一个基于当前知识的评估
            if (simulatedKnowledges.Count == 0)
            {
                simulatedKnowledges.Add(Knowledge.Clone());
            }

            return simulatedKnowledges;
        }

        /// <summary>
        /// 找到内部的、且不可能被对手占据的气
        /// </summary>
        private List<Point> FindInnerEyes(GoBoard selfBoard)
        {
            var innerEyes = new List<Point>();
            for (int x = 1; x <= _boardSize; x++)
            {
                for (int y = 1; y <= _boardSize; y++)
                {
                    var p = new Point(x, y);

                    // 1. 必须是我方的一个"简单眼"
                    if (!PlayerColor.CompareToPointState(selfBoard.IsEyeish(p)))
                    {
                        continue;
                    }

                    // 2. 检查对角线，防止"假眼"
                    var diagonals = new[]
                    {
                        new Point(p.Row - 1, p.Col - 1), new Point(p.Row - 1, p.Col + 1),
                        new Point(p.Row + 1, p.Col + 1), new Point(p.Row + 1, p.Col - 1)
                    };

                    int faults = 0; // 对角线上的缺陷数（不是己方棋子的位置）
                    bool isSide = false; // 是否在边上
                    foreach (var j in diagonals)
                    {
                        if (j.Row >= 1 && j.Row <= _boardSize && j.Col >= 1 && j.Col <= _boardSize)
                        {
                            // 如果对角线位置不是己方棋子（即是空位或对手棋子），就是缺陷
                            if (!PlayerColor.CompareToPointState(selfBoard.GetPointState(j)))
                            {
                                faults++;
                            }
                        }
                        else
                        {
                            isSide = true;
                        }
                    }

                    // 如果对角线缺陷太多，则这个眼是假眼，跳过。
                    // 在中心位置：缺陷 >= 2 是假眼
                    // 在边上：有任何缺陷 (faults > 0) 是假眼
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
        /// 获取一个只包含自己棋子的 GoBoard
        /// </summary>
        private GoBoard GetSelfBoard()
        {
            GoBoard board = new GoBoard();
            for (int x = 1; x <= _boardSize; x++)
            {
                for (int y = 1; y <= _boardSize; y++)
                {
                    var p = new Point(x, y);
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
            for (int x = 1; x <= _boardSize; x++)
            {
                for (int y = 1; y <= _boardSize; y++)
                {
                    var p = new Point(x, y);
                    if (Knowledge.GetMemoryState(p) == MemoryPointState.Self)
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
        /// 将 (x, y) 坐标 (1-based) 转换为扁平索引 (0-based)
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