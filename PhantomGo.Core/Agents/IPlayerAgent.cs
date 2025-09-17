using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;

namespace PhantomGo.Core.Agents
{
    /// <summary>
    /// 玩家代理接口，可以是人类输入、AI决策
    /// </summary>
    public interface IPlayerAgent
    {
        /// <summary>
        /// 玩家的棋局知识库
        /// </summary>
        PlayerKnowledge Knowledge { get; }
        /// <summary>
        /// 生成一个落子点
        /// </summary>
        /// <param name="gameView">当前玩家的棋局视图</param>
        /// <returns>决策出的落子点</returns>
        Point GenerateMove(IGameView gameView, PlayerKnowledge knowledge);
        /// <summary>
        /// 根据裁判返回的信息，更新记忆
        /// </summary>
        /// <param name="point"></param>
        /// <param name="result"></param>
        void UpdateKnowledge(Point point, PlayResult result);
        /// <summary>
        /// 根据历史记录重建记忆
        /// </summary>
        void RebuildKnowledgeFromHistory(List<MoveRecord> gameHisotry, Player player)
        {
            Knowledge.Clear();
            foreach(var record in gameHisotry)
            {
                // 如果是己方落子
                if(record.player == player)
                {
                    if(record.result.IsSuccess && record.point.isMove())
                    {
                        Knowledge.AddOwnState(record.point);
                    } else if(!record.result.IsSuccess && record.point.isMove())
                    {
                        Knowledge.MarkAsInferred(record.point);
                    }
                } 
                // 如果是对方棋子且我的棋子被提了
                else
                {
                    if(record.result.IsSuccess && record.point.isMove() && record.result.CapturedPoints.Count > 0)
                    {
                        foreach(var point in record.result.CapturedPoints)
                        {
                            Knowledge.RemoveState(point);
                        }
                    }
                }
            }
        }
    }
}
