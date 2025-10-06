using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示对局中的玩家
    /// </summary>
    public enum Player
    {
        /// <summary>
        /// 执黑子的玩家
        /// </summary>
        Black = 1,
        /// <summary>
        /// 执白子的玩家
        /// </summary>
        White = 2,
    }
    
    /// <summary>
    /// 提供与 Player 有关的扩展方法
    /// </summary>
    public static class PlayerHelper
    {
        /// <summary>
        /// 获取当前玩家的对手颜色
        /// </summary>
        public static Player GetOpponent(this Player player)
        {
            return player == Player.Black ? Player.White : Player.Black;
        }
        public static bool CompareToPointState(this Player player, PointState pointState)
        {
            if(pointState == PointState.black && player == Player.Black ||
               pointState == PointState.white && player == Player.White)
            {
                return true;
            }
            return false;
        }
        public static string ToString(this Player player)
        {
            return player == Player.Black ? "⚫ " : "⚪ ";
        }
    }
}
