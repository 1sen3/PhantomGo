using PhantomGo.Core.Agents;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;

namespace PhantomGo.Core.Agents
{
    /// <summary>
    /// 一个简单的幻影围棋 AI Agent
    /// 它的决策基于一个通过观察得到的最佳猜测棋盘
    /// </summary>
    public class SimpleAgentPlayer : IPlayerAgent
    {
        public PlayerKnowledge Knowledge { get; }
        public Player PlayerColor { get; }

        // 九路棋盘的初始位置价值矩阵
        private static readonly double[,] InitialPositionBonus = new double[10, 10]
        {   
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, // 占位行，实际棋盘从1开始
            { 0, 1, 2, 3, 4, 5, 4, 3, 2, 1 }, // Y=1 (第1行)
            { 0, 2, 3, 4, 5, 6, 5, 4, 3, 2 }, // Y=2 (第2行)
            { 0, 3, 4, 8, 6, 7, 6, 8, 4, 3 }, // Y=3 (第3行, 3-3, 3-7 是星位)
            { 0, 4, 5, 6, 7, 8, 7, 6, 5, 4 }, // Y=4 (第4行)
            { 0, 5, 6, 7, 8, 9, 8, 7, 6, 5 }, // Y=5 (第5行, 天元)
            { 0, 4, 5, 6, 7, 8, 7, 6, 5, 4 }, // Y=6 (第6行)
            { 0, 3, 4, 8, 6, 7, 6, 8, 4, 3 }, // Y=7 (第7行, 7-3, 7-7 是星位)
            { 0, 2, 3, 4, 5, 6, 5, 4, 3, 2 }, // Y=8 (第8行)
            { 0, 1, 2, 3, 4, 5, 4, 3, 2, 1 }  // Y=9 (第9行)
        };
        public SimpleAgentPlayer(int boardSize, Player playerColor)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            PlayerColor = playerColor;
        }
        /// <summary>
        /// 根据评估函数，遍历所有可能落子点，选择得分最高的点
        /// </summary>
        public Point GenerateMove(IGameView gameView, PlayerKnowledge knowledge)
        {
            Point bestMove = Point.Pass();
            GoBoard bestGuessBoard = knowledge.GetBestGuessBoard(PlayerColor);
            double bestScore = double.MinValue;

            // 设置一个权重来平衡局面分数和位置分数
            // 在开局时，位置分数更重要；后期则局面分数更重要
            const double positionalWeight = 0.5; // 权重可以调整

            for (int x = 1;x <= gameView.BoardSize;++x)
            {
                for(int y = 1;y <= gameView.BoardSize;++y)
                {
                    Point point = new Point(x, y);
                    if(knowledge.GetMemoryState(point) != MemoryPointState.Unknown)
                    {
                        continue;
                    }
                    // 模拟落子
                    GoBoard tmpBoard = bestGuessBoard.Clone();
                    tmpBoard.PlaceStone(point, PlayerColor);
                    // 评估落子后的局面分数
                    double score = Evaluate(tmpBoard);
                    double positionalBonus = InitialPositionBonus[x, y];
                    score += positionalBonus * positionalWeight;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = point;
                    }
                }
            }
            return bestMove;
        }
        /// <summary>
        /// 局面评估函数
        /// 评估当前棋盘对当前玩家的有利程度
        /// </summary>
        private double Evaluate(GoBoard board)
        {
            Player opponentPlayer = PlayerColor.GetOpponent();
            int myStones = 0;
            int opponentStones = 0;

            // 计算棋子数量
            for (int x = 1; x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    var pointState = board.GetPointState(new Point(x, y));
                    if (PlayerColor.CompareToPointState(pointState))
                    {
                        myStones++;
                    } else if(opponentPlayer.CompareToPointState(pointState))
                    {
                        opponentStones++;
                    }
                }
            }

            double stoneScores = myStones - opponentStones;

            // 计算领地
            var (myTerritory, opponentTerritory) = CalculateTerritory(board);
            double territoryScores = myTerritory - opponentTerritory;

            // 后续可以调整权重
            return stoneScores + territoryScores * 0.5;
        }
        /// <summary>
        /// 使用 BFS 计算双方领地
        /// </summary>

        private (int myTerritory, int opponentTerritory) CalculateTerritory(GoBoard board)
        {
            Player opponentPlayer = PlayerColor.GetOpponent();
            int myTerritory = 0, opponentTerritory = 0;
            bool[,] visited = new bool[board.Size + 1, board.Size + 1];

            for(int x = 1;x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    if (board.GetPointState(new Point(x, y)) == PointState.None && !visited[x, y])
                    {
                        List<Point> area = new List<Point>();
                        bool touchesMyStones = false;
                        bool touchesOppoStones = false;

                        Queue<Point> queue = new Queue<Point>();
                        queue.Enqueue(new Point(x, y));
                        while (queue.Count > 0)
                        {
                            Point current = queue.Dequeue();
                            area.Add(current);
                            foreach (var neighbor in board.GetNeighbors(current))
                            {
                                var neighborState = board.GetPointState(neighbor);
                                if (neighborState == PointState.None)
                                {
                                    if (!visited[neighbor.X, neighbor.Y])
                                    {
                                        visited[neighbor.X, neighbor.Y] = true;
                                        queue.Enqueue(neighbor);
                                    }
                                }
                                else if (PlayerColor.CompareToPointState(neighborState))
                                {
                                    touchesMyStones = true;
                                }
                                else if (opponentPlayer.CompareToPointState(neighborState))
                                {
                                    touchesOppoStones = true;
                                }
                            }
                        }
                        if (touchesMyStones && !touchesOppoStones)
                        {
                            myTerritory += area.Count;
                        }
                        else if (touchesOppoStones && !touchesMyStones)
                        {
                            opponentTerritory += area.Count;
                        }
                    }
                }
            }
            return (myTerritory, opponentTerritory);
        }
    }
}
