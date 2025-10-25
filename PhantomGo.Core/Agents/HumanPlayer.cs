using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Agents;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;

namespace PhantomGo.Core.Agents
{
    public class HumanPlayer : IPlayerAgent
    {
        public Dictionary<Player, int> MoveCount { get; set; }
        public PlayerKnowledge Knowledge { get; set; }
        public Player PlayerColor { get; }
        public HumanPlayer(int boardSize, Player playerColor)
        {
            Knowledge = new PlayerKnowledge(boardSize, playerColor);
            PlayerColor = playerColor;
        }
        public (double, Point) GenerateMove()
        {
            return (0, Point.Pass());
        }
        public override string ToString()
        {
            return "HumanPlayer";
        }
        public void OnMoveSuccess()
        {
            return;
        }
        public void OnMoveFailed()
        {
            return;
        }
        public void OnPointCaptured(List<Point> points)
        {
            return;
        }
    }
}
