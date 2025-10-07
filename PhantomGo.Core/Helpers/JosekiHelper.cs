using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Helpers;

/// <summary>
/// 提供围棋定式开局走法
/// </summary>
public static class JosekiHelper
{
    /// <summary>
    /// 定式库，按优先级排序
    /// </summary>
    private static readonly List<Point> _openingBookPoints = new List<Point>
    {
        new Point(5, 5), // 天元
        new Point(3, 3), new Point(7, 7), new Point(3, 7), new Point(7, 3), // 星位
        new Point(5, 3), new Point(3, 5), new Point(7, 5), new Point(5, 7) // 边星
    };

    /// <summary>
    /// 获取一个定式落子点
    /// </summary>
    /// <param name="moveCount">当前已落子手数</param>
    /// <param name="knowledge">当前玩家的知识库</param>
    /// <param name="player">当前玩家颜色</param>
    /// <returns>如果找到定式落子点则返回该点，否则返回null</returns>
    public static Point? GetJosekiMove(int moveCount, PlayerKnowledge knowledge, Player player)
    {
        if (moveCount >= 5) return null;

        // 获取当前玩家的猜测棋盘
        var bestGuessBoard = knowledge.GetBestGuessBoard(player);

        foreach(var point in _openingBookPoints)
        {
            if(knowledge.GetMemoryState(point) == MemoryPointState.Unknown)
            {
                // 检查定式位置在我的认知中是否为空
                if (bestGuessBoard.IsValidMove(point, player))
                {
                    // 在猜测棋盘上验证此步是否合法，防止自填真眼等情况
                    System.Diagnostics.Debug.WriteLine($"[JosekiHelper] 为 {player} 在第 {moveCount + 1} 手提供定式: {point}");
                    return point;
                }
            }
        }

        // 如果所有定式点都不可用，则返回null
        return null;
    }
}
