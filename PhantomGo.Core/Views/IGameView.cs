using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Agents;

namespace PhantomGo.Core.Views
{
    public interface IGameView
    {
        Player CurrentPlayer { get; }
        int BoardSize { get; }
        PointState GetPointState(Point point);
        PlayResult MakeMove(Point point);
    }
}
