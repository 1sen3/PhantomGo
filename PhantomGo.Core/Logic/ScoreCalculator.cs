using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Models;

namespace PhantomGo.Core.Logic
{
    public record ScoreResult(double BlackScore, double WhiteScore, Player Winner)
    {
        public double Margin => Math.Abs(BlackScore - WhiteScore);
    }
    public class ScoreCalculator
    {
        private readonly GoBoard _board;
        private readonly bool[,] _visited;
        private const double Komi = 7.5; // 贴目
        public ScoreCalculator(GoBoard board)
        {
            _board = board;
            _visited = new bool[board.Size + 1, board.Size + 1];
        }
        public ScoreResult CalculateScores()
        {
            double blackScore = 0;
            double whiteScore = 0;
            for(int x = 1;x <= _board.Size;++x)
            {
                for (int y = 1; y <= _board.Size; ++y)
                {
                    var color = _board.GetPointState(new Point(x, y));
                    if (color == PointState.white)
                    {
                        whiteScore++;
                        _visited[x, y] = true;
                    }
                    else if (color == PointState.black)
                    {
                        blackScore++;
                        _visited[x, y] = true;
                    }
                    else
                    {
                        var (region, borderColors) = FindEmptyRegion(new Point(x, y));
                        if (borderColors.Count == 1)
                        {
                            if (borderColors.Contains(PointState.black))
                            {
                                blackScore += region.Count;
                            }
                            else if (borderColors.Contains(PointState.white))
                            {
                                whiteScore += region.Count;
                            }
                        }
                    }
                }
            }
            whiteScore += Komi;
            return whiteScore > blackScore ? new ScoreResult(blackScore, whiteScore, Player.White) : new ScoreResult(blackScore, whiteScore, Player.Black);
        }
        private (HashSet<Point> region, HashSet<PointState> borderColors) FindEmptyRegion(Point startPoint)
        {
            var region = new HashSet<Point>();
            var borderColors = new HashSet<PointState>();
            var queue = new Queue<Point>();

            if (_visited[startPoint.X, startPoint.Y])
            {
                return (region, borderColors);
            }
            queue.Enqueue(startPoint);
            _visited[startPoint.X, startPoint.Y] = true;
            while(queue.Count > 0)
            {
                var current = queue.Dequeue();
                region.Add(current);
                foreach (var neighbor in _board.GetNeighbors(current))
                {
                    var neighborState = _board.GetPointState(neighbor);
                    if (neighborState == PointState.None)
                    {
                        if (!_visited[neighbor.X, neighbor.Y])
                        {
                            _visited[neighbor.X, neighbor.Y] = true;
                            queue.Enqueue(neighbor);
                        }
                    } else
                    {
                        borderColors.Add(neighborState);
                    }
                }
            }
            return (region, borderColors);
        }
    }
}
