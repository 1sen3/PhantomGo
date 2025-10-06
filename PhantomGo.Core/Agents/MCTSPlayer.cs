using PhantomGo.Core.Models;
using PhantomGo.Core.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Agents
{
    public class MCTSPlayer : IPlayerAgent
    {
        public PlayerKnowledge Knowledge { get; }
        public Player PlayerColor { get; }
        public List<Point> Eyes { get; private set; }
        public MCTSPlayer(int boardSize, Player playerColor)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            PlayerColor = playerColor;
            Eyes = new List<Point>();
        }
        public Point GenerateMove()
        {
            return Point.Pass();
        }

        #region MCTS 节点类定义
        internal class MCTSNode
        {
            public MCTSNode Parent { get; }
        }
        #endregion
    }
}