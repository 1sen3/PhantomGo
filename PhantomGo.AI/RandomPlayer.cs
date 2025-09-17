using System.Runtime.CompilerServices;
using PhantomGo.Core.Agents;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;

namespace PhantomGo.AI
{
    public class RandomPlayer : IPlayerAgent
    {
        public PlayerKnowledge Knowledge { get; }
        public RandomPlayer(int boardSize)
        {
            Knowledge = new PlayerKnowledge(boardSize);
        }
        private readonly Random _random = new Random();
        public Point GenerateMove(IGameView gameView, PlayerKnowledge knowledge)
        {
            var boardSize = gameView.BoardSize;
            var validMoves = new List<Point>();
            for(int x = 1;x <= boardSize;++x)
            {
                for(int y = 1;y <= boardSize; ++y)
                {
                    var point = new Point(x, y);
                    if(knowledge.GetMemoryState(point) == MemoryPointState.Unknown)
                    {
                        validMoves.Add(point);
                    }
                }
            }
            if(validMoves.Count > 0)
            {
                int index = _random.Next(validMoves.Count);
                return validMoves[index];
            } else
            {
                // pass
                return new Point(0, 0);
            }
        }
        public void UpdateKnowledge(Point point, PlayResult result)
        {
            if(result.IsSuccess)
            {
                Knowledge.AddOwnState(point);
            } else
            {
                Knowledge.MarkAsInferred(point);
            }
        }
    }
}