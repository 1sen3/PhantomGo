using PhantomGo.Core.Helpers;
using PhantomGo.Core.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 玩家对棋盘的认知状态
    /// </summary>
    public enum MemoryPointState
    {
        Unknown, // 未知
        Self, // 己方棋子
        InferredOpponent, // 推测的对方棋子
        KoBlocked, // 劫禁点
    }
    /// <summary>
    /// 存储玩家对棋盘的记忆
    /// </summary>
    public class PlayerKnowledge
    {
        private MemoryPointState[,] _memeryBoard;
        public int BoardSize { get; }
        public Player PlayerColor { get; }
        public PlayerKnowledge(Player playerColor)
        {
            BoardSize = 9;
            PlayerColor = playerColor;
            _memeryBoard = new MemoryPointState[BoardSize + 1, BoardSize + 1];
        }
        /// <summary>
        /// 获取某个点的记忆状态
        /// </summary>
        public MemoryPointState GetMemoryState(Point point)
        {
            return _memeryBoard[point.Row, point.Col];
        }
        /// <summary>
        /// 落子成功时，更新记忆状态
        /// </summary>
        /// <param name="point"></param>
        public void AddOwnState(Point point)
        {
            _memeryBoard[point.Row, point.Col] = MemoryPointState.Self;
        }
        /// <summary>
        /// 落子失败时，更新记忆状态
        /// </summary>
        public void MarkAsInferred(Point point)
        {
            _memeryBoard[point.Row, point.Col] = MemoryPointState.InferredOpponent;
        }

        public void MarkAsKoBlocked(Point point)
        {
            _memeryBoard[point.Row, point.Col] = MemoryPointState.KoBlocked;
        }
        public void OnPointCaptured(IReadOnlyList<Point> capturedPoints)
        {
            var capturedSets = new HashSet<Point>(capturedPoints);

            foreach(var point in capturedPoints)
            {
                RemoveState(point);
                var neighbors = new List<Point>
                {
                    new Point(point.Row - 1, point.Col),
                    new Point(point.Row + 1, point.Col),
                    new Point(point.Row, point.Col - 1),
                    new Point(point.Row, point.Col + 1)
                }.Where(p => p.Row >= 1 && p.Row <= BoardSize && p.Col >= 1 && p.Col <= BoardSize);
                foreach(var neighbor in neighbors)
                {
                    if(GetMemoryState(neighbor) == MemoryPointState.Unknown && !capturedSets.Contains(neighbor))
                    {
                        //Debug.Write($"{neighbor} 被标记为对方棋子");
                        MarkAsInferred(neighbor);
                    }
                }
            }
        }
        public PlayResult MakeMove(Point point)
        {
            var board = TransMemoryToBoard();
            var result = board.PlaceStone(point, PlayerColor);
            _memeryBoard = TransBoardToMemory(board);
            return result;
        }
        /// <summary>
        /// 移除记忆状态
        /// </summary>
        public void RemoveState(Point point)
        {
            _memeryBoard[point.Row, point.Col] = MemoryPointState.Unknown;
        }
        /// <summary>
        /// 清空所有记忆
        /// </summary>
        public void Clear()
        {
            for (int row = 1; row <= BoardSize; row++)
            {
                for (int col = 1; col <= BoardSize; col++)
                {
                    _memeryBoard[row, col] = MemoryPointState.Unknown;
                }
            }
        }
        /// <summary>
        /// 根据当前记忆，构建并返回一个最佳猜测棋盘
        /// </summary>
        /// <param name="currentPlayer">当前 Agent 的颜色</param>
        /// <returns>一个代表当前局势猜测的 GoBoard 对象</returns>
        public GoBoard GetBestGuessBoard(Player currentPlayer)
        {
            GoBoard guessBoard = new GoBoard();
            Player opponentPlayer = currentPlayer.GetOpponent();
            for(int row = 1; row <= BoardSize; ++row)
            {
                for(int col = 1; col <= BoardSize; ++col)
                {
                    var point = new Point(row, col);
                    if(GetMemoryState(point) == MemoryPointState.Self)
                    {
                        guessBoard.PlaceStone(point, currentPlayer);
                    } else if(GetMemoryState(point) == MemoryPointState.InferredOpponent)
                    {
                        guessBoard.PlaceStone(point, opponentPlayer);
                    }
                }
            }
            return guessBoard;
        }
        public GoBoard TransMemoryToBoard()
        {
            GoBoard board = new GoBoard();
            for(int row = 1; row <= BoardSize; ++row)
            {
                for(int col = 1; col <= BoardSize; ++col)
                {
                    var point = new Point(row, col);
                    if (GetMemoryState(point) == MemoryPointState.Self) board.SetState(point, PlayerColor);
                    else if(GetMemoryState(point) == MemoryPointState.InferredOpponent) board.SetState(point, PlayerColor.GetOpponent());
                }
            }
            return board;
        }
        public MemoryPointState[,] TransBoardToMemory(GoBoard board)
        {
            var memory = new MemoryPointState[BoardSize + 1, BoardSize + 1];
            for (int row = 1; row <= BoardSize; ++row)
            {
                for (int col = 1; col <= BoardSize; ++col)
                {
                    var point = new Point(row, col);
                    var pointState = board.GetPointState(point);
                    if (pointState == PointState.None) continue;
                    else if (PlayerColor.CompareToPointState(pointState)) memory[point.Row, point.Col] = MemoryPointState.Self;
                    else memory[point.Row, point.Col] = MemoryPointState.InferredOpponent;
                }
            }
            return memory;
        }
        public PlayerKnowledge Clone()
        {
            var newKnowledge = new PlayerKnowledge(PlayerColor);
            Array.Copy(this._memeryBoard, newKnowledge._memeryBoard, this._memeryBoard.Length);
            return newKnowledge;
        }
    }
}
