using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Logic;

namespace PhantomGo.Core.Models
{
    public class GameStateRecord
    {
        public GoBoard Board { get; }
        public Player CurrentPlayer { get; }
        public Dictionary<Player, int> CapturedPointCounts { get; }
        public int ConsecutivePasses { get; }
        public GameStateRecord(GoBoard board, Player currentPlayer, Dictionary<Player, int> capturedPointCounts, int consecutivePasses)
        {
            Board = board.Clone();
            CurrentPlayer = currentPlayer;
            CapturedPointCounts = new Dictionary<Player, int>(capturedPointCounts);
            ConsecutivePasses = consecutivePasses;
        }
    }
}
