using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Logic;

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
    }
    /// <summary>
    /// 存储玩家对棋盘的记忆
    /// </summary>
    public class PlayerKnowledge
    {
        private readonly MemoryPointState[,] _memeryBoard;
        public int BoardSize { get; }
        public PlayerKnowledge(int boardSize)
        {
            BoardSize = boardSize;
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
        /// <summary>
        /// 自己的棋子被提子时，移除记忆状态
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
    }
}
