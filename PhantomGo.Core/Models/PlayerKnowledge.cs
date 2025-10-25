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
        public PlayerKnowledge(int boardSize, Player playerColor)
        {
            BoardSize = boardSize;
            PlayerColor = playerColor;
            _memeryBoard = new MemoryPointState[boardSize + 1, boardSize + 1];
        }
        /// <summary>
        /// 获取某个点的记忆状态
        /// </summary>
        public MemoryPointState GetMemoryState(Point point)
        {
            return _memeryBoard[point.X, point.Y];
        }
        /// <summary>
        /// 落子成功时，更新记忆状态
        /// </summary>
        /// <param name="point"></param>
        public void AddOwnState(Point point)
        {
            _memeryBoard[point.X, point.Y] = MemoryPointState.Self;
        }
        /// <summary>
        /// 落子失败时，更新记忆状态
        /// </summary>
        public void MarkAsInferred(Point point)
        {
            _memeryBoard[point.X, point.Y] = MemoryPointState.InferredOpponent;
        }

        public void MarkAsKoBlocked(Point point)
        {
            _memeryBoard[point.X, point.Y] = MemoryPointState.KoBlocked;
        }
        public void OnPointCaptured(IReadOnlyList<Point> capturedPoints)
        {
            var capturedSets = new HashSet<Point>(capturedPoints);

            foreach(var point in capturedPoints)
            {
                RemoveState(point);
                var neighbors = new List<Point>
                {
                    new Point(point.X - 1, point.Y),
                    new Point(point.X + 1, point.Y),
                    new Point(point.X, point.Y - 1),
                    new Point(point.X, point.Y + 1)
                }.Where(p => p.X >= 1 && p.X <= BoardSize && p.Y >= 1 && p.Y <= BoardSize);
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
        public void MakeMove(Point point)
        {
            var board = TransMemoryToBoard();
            board.PlaceStone(point, PlayerColor);
            _memeryBoard = TransBoardToMemory(board);
        }
        /// <summary>
        /// 移除记忆状态
        /// </summary>
        public void RemoveState(Point point)
        {
            _memeryBoard[point.X, point.Y] = MemoryPointState.Unknown;
        }
        /// <summary>
        /// 清空所有记忆
        /// </summary>
        public void Clear()
        {
            for (int x = 1; x <= BoardSize; x++)
            {
                for (int y = 1; y <= BoardSize; y++)
                {
                    _memeryBoard[x, y] = MemoryPointState.Unknown;
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
            for(int x = 1;x <= BoardSize;++x)
            {
                for(int y = 1;y <= BoardSize;++y)
                {
                    var point = new Point(x, y);
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
            for(int x = 1;x <= BoardSize;++x)
            {
                for(int y = 1;y <= BoardSize;++y)
                {
                    var point = new Point(x, y);
                    if (GetMemoryState(point) == MemoryPointState.Self) board.SetState(point, PlayerColor);
                    else if(GetMemoryState(point) == MemoryPointState.InferredOpponent) board.SetState(point, PlayerColor.GetOpponent());
                }
            }
            return board;
        }
        public MemoryPointState[,] TransBoardToMemory(GoBoard board)
        {
            var memory = new MemoryPointState[BoardSize + 1, BoardSize + 1];
            for (int x = 1; x <= BoardSize; ++x)
            {
                for (int y = 1; y <= BoardSize; ++y)
                {
                    var point = new Point(x, y);
                    var pointState = board.GetPointState(point);
                    if (pointState == PointState.None) continue;
                    else if (PlayerColor.CompareToPointState(pointState)) memory[point.X, point.Y] = MemoryPointState.Self;
                    else memory[point.X, point.Y] = MemoryPointState.InferredOpponent;
                }
            }
            return memory;
        }
        public PlayerKnowledge Clone()
        {
            var newKnowledge = new PlayerKnowledge(BoardSize, PlayerColor);
            Array.Copy(this._memeryBoard, newKnowledge._memeryBoard, this._memeryBoard.Length);
            return newKnowledge;
        }
    }
}
