using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Logic
{
    /// <summary>
    /// 评估棋面价值的评估器
    /// </summary>
    public class Evaluator
    {
        private const double TerritoryWeight = 0.6;
        private const double EyeWeight = 1.5;
        private const double ConnectivityWeight = 0.8;

        /// <summary>
        /// 主评估函数
        /// </summary>
        public double Evaluate(GoBoard board, Player player)
        {
            var opponent = player.GetOpponent();

            double territoryScore = (CalculateTerritory(board, player) - CalculateTerritory(board, opponent)) * TerritoryWeight;
            double eyeScore = (CountEyes(board, player) - CountEyes(board, opponent)) * EyeWeight;
            double connectivityScore = CalculateConnectivity(board, player) * ConnectivityWeight;

            return territoryScore + eyeScore + connectivityScore;
        }

        /// <summary>
        /// 计算领土面积
        /// </summary>
        private int CalculateTerritory(GoBoard board, Player player)
        {
            int territory = 0;
            bool[,] visited = new bool[board.Size + 1, board.Size + 1];
            var playerColor = player == Player.Black ? PointState.black : PointState.white;
            var opponentColor = player == Player.Black ? PointState.white : PointState.black;

            for(int x = 1;x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    if(board.GetPointState(new Point(x, y)) == PointState.None && !visited[x, y]) {
                        var area = new List<Point>();
                        bool touchesPlayer = false;
                        bool touchesOpponent = false;
                        var que = new Queue<Point>();

                        que.Enqueue(new Point(x, y));
                        visited[x, y] = true;

                        while(que.Any())
                        {
                            var current = que.Dequeue();
                            area.Add(current);
                            foreach(var neighbor in board.GetNeighbors(current))
                            {
                                var neighborState = board.GetPointState(neighbor);
                                if (neighborState == PointState.None)
                                {
                                    if (!visited[neighbor.X, neighbor.Y])
                                    {
                                        visited[neighbor.X, neighbor.Y] = true;
                                        que.Enqueue(neighbor);
                                    }
                                }
                                else if (neighborState == playerColor) touchesPlayer = true;
                                else if (neighborState == opponentColor) touchesOpponent = true;
                            }
                        }

                        if (touchesPlayer && !touchesOpponent) territory += area.Count;
                    }
                }
            }
            return territory;
        }
        /// <summary>
        /// 计算真眼数量
        /// </summary>
        private int CountEyes(GoBoard board, Player player)
        {
            int eyeCount = 0;
            var playerColor = player == Player.Black ? PointState.black : PointState.white;
            var opponentColor = player == Player.Black ? PointState.white : PointState.black;

            for (int x = 1;x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    var point = new Point(x, y);
                    if(board.GetPointState(point) == PointState.None) {
                        int myPoints = 0;
                        var oppoPoints = new List<Point>();
                        foreach(var neighbor in board.GetNeighbors(point))
                        {
                            var neighborState = board.GetPointState(neighbor);
                            if (neighborState == playerColor) myPoints++;
                            else if (neighborState == opponentColor) oppoPoints.Add(neighbor);
                        }

                        // 根据位置判断所需的最少己方棋子数
                        int requiredPoints = GetRequiredPointsForEye(board, point);
                        if(myPoints >= requiredPoints)
                        {
                            // 如果邻居中对方棋子数大于 0，检查每个对方棋子的气
                            if (oppoPoints.Count > 0)
                            {
                                var liveOppoPoints = new List<Point>();
                                foreach (var oppoPoint in oppoPoints)
                                {
                                    var liberty = board.GetLiberty(oppoPoint);
                                    if (liberty > 1) liveOppoPoints.Add(oppoPoint);
                                }

                                if (liveOppoPoints.Count == 0) eyeCount++;
                            }
                            else eyeCount++;
                        }
                    }
                }
            }

            return eyeCount;
        }
        /// <summary>
        /// 获取真眼需要的己方点数
        /// </summary>
        private int GetRequiredPointsForEye(GoBoard board, Point point)
        {
            int neighborsCount = board.GetNeighbors(point).Count();
            if (neighborsCount == 2) return 2;
            else if (neighborsCount == 3) return 3;
            else if (neighborsCount == 4) return 4;
            else return 4;
        }
        /// <summary>
        /// 计算连通性分数
        /// </summary>
        private double CalculateConnectivity(GoBoard board, Player player)
        {
            double connectivityScore = 0;
            var playerColor = player == Player.Black ? PointState.black : PointState.white;

            for(int x = 1;x <= board.Size;++x)
            {
                for(int y = 1;y <= board.Size;++y)
                {
                    var point = new Point(x, y);
                    if (board.GetPointState(point) == playerColor)
                    {
                        foreach (var neighbor in board.GetNeighbors(point))
                        {
                            if (board.GetPointState(neighbor) == playerColor)
                            {
                                connectivityScore += 0.25;
                            }
                        }
                        foreach(var diagonal in board.GetDiagonals(point))
                        {
                            if(board.GetPointState(diagonal) == playerColor)
                            {
                                connectivityScore += 0.15;
                            }
                        }
                    }
                }
            }

            return connectivityScore / 2.0;
        }
    }
}
