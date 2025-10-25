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
        Human = 0,
        //SimpleAgentPlayer = 1,
        AI = 1,
    }

    public static class AgentEnumExtensions
    {
        public static IPlayerAgent ToPlayerAgent(this AgentEnum playerEnum, int boardSize, PhantomGo.Core.Models.Player playerColor)
        {
            return playerEnum switch
            {
                AgentEnum.Human => new HumanPlayer(boardSize, playerColor),
                //AgentEnum.SimpleAgentPlayer => new SimpleAgentPlayer(boardSize, playerColor),
                AgentEnum.AI => new AIPlayer(boardSize, playerColor),
                _ => throw new ArgumentOutOfRangeException(nameof(playerEnum), $"Not expected player enum value: {playerEnum}"),
            };
        }
    }
}
