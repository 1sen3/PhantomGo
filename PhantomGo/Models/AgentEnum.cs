using PhantomGo.Core.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Models
{
    public enum AgentEnum
    {
        HumanPlayer = 0,
        //SimpleAgentPlayer = 1,
        MCTSPlayer = 1,
    }

    public static class AgentEnumExtensions
    {
        public static IPlayerAgent ToPlayerAgent(this AgentEnum playerEnum, int boardSize, PhantomGo.Core.Models.Player playerColor)
        {
            return playerEnum switch
            {
                AgentEnum.HumanPlayer => new HumanPlayer(boardSize, playerColor),
                //AgentEnum.SimpleAgentPlayer => new SimpleAgentPlayer(boardSize, playerColor),
                AgentEnum.MCTSPlayer => new MCTSPlayer(boardSize, playerColor),
                _ => throw new ArgumentOutOfRangeException(nameof(playerEnum), $"Not expected player enum value: {playerEnum}"),
            };
        }
    }
}
