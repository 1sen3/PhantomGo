﻿using PhantomGo.Core.Agents;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;
using PhantomGo.Core.Helpers;

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
        public int MoveCount { get; set; }
        private readonly Evaluator _evaluator;

        public SimpleAgentPlayer(int boardSize, Player playerColor)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            MoveCount = 0;
            PlayerColor = playerColor;
            _evaluator = new Evaluator();
        }
        /// <summary>
        /// 根据评估函数，遍历所有可能落子点，选择得分最高的点
        /// </summary>
        public Point GenerateMove()
        {
            GoBoard bestGuessBoard = Knowledge.GetBestGuessBoard(PlayerColor);

            // 1. 尝试从 JosekiHelper 中获取一个定式落子点
            var josekiPoint = JosekiHelper.GetJosekiMove(MoveCount, Knowledge, PlayerColor);
            if(josekiPoint.HasValue)
            {
                return josekiPoint.Value;
            }

            // 2. 没有定式点，进入中盘评估
            System.Diagnostics.Debug.WriteLine($"[GenerateMove] {PlayerColor} 决策，当前是第 {MoveCount + 1} 手 (中盘)");

            Point bestMove = Point.Pass();
            double bestScore = double.MinValue;

            int candidateCount = 0;

            for (int x = 1;x <= Knowledge.BoardSize;++x)
            {
                for(int y = 1;y <= Knowledge.BoardSize;++y)
                {
                    Point point = new Point(x, y);
                    if (Knowledge.GetMemoryState(point) != MemoryPointState.Unknown)
                    {
                        continue;
                    }

                    if (!bestGuessBoard.IsValidMove(point, PlayerColor))
                    {
                        continue;
                    }

                    candidateCount++;
                    
                    // 模拟落子
                    GoBoard tmpBoard = bestGuessBoard.Clone();
                    tmpBoard.PlaceStone(point, PlayerColor);
                    // 评估落子后的局面分数
                    double score = _evaluator.Evaluate(tmpBoard, PlayerColor);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = point;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[GenerateMove] {PlayerColor} 找到 {candidateCount} 个候选位置，选择: {bestMove}");
            return bestMove;
        }
        public override string ToString()
        {
            return "AgentPlayer";
        }
    }
}
