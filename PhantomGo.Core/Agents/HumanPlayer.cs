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
        public PlayerKnowledge Knowledge { get; }
        public Player PlayerColor { get; }
        public HumanPlayer(int boardSize, Player playerColor)
        {
            Knowledge = new PlayerKnowledge(boardSize);
            PlayerColor = playerColor;
        }
        public Point GenerateMove(IGameView gameView, PlayerKnowledge knowledge)
        {
            while(true)
            {
                Console.Write("请输入落子点（A1）、'pass'、'undo'或'quit'：");
                string input = Console.ReadLine().ToLower().Trim();
                if (input == "pass")
                {
                    return new Point(0, 0);
                } else if (input == "undo")
                {
                    return new Point(0, 1);
                } else if (input == "quit")
                {
                    return new Point(0, 2);
                } else {
                    try
                    {
                        return Point.TransInputToPoint(input);
                    } catch(ArgumentException e)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("输入不合法，请重试");
                        Console.ResetColor();
                    }
                }
            }
        }
    }
}
