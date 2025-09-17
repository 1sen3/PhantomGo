using System.Collections.Generic;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;

namespace PhantomGo.Core.Views
{
    public class PhantomGoView : IGameView
    {
        private readonly GameController _gameController;
        private readonly Player _player;
        private readonly GoBoard _playerBoard;
        public PhantomGoView(GameController game, Player player)
        {
            _gameController = game;
            _player = player;
            _playerBoard = new GoBoard(game.BoardSize);
            for(int x = 1;x <= _playerBoard.Size;++x)
            {
                for(int y = 1;y <= _playerBoard.Size;++y)
                {
                    var point = new Point(x, y);
                    var state = _gameController.GetPointState(point);
                    if(state == PointState.black && player == Player.Black || state == PointState.white && player == Player.White)
                    {
                        _playerBoard.PlaceStone(point, player);
                    }
                }
            }
        }
        public Player CurrentPlayer => _player;
        public int BoardSize => _gameController.BoardSize;
        public PointState GetPointState(Point point)
        {
            var trueState = _gameController.GetPointState(point);
            var playerState = _player == Player.Black ? PointState.black : PointState.white;
            if(trueState == playerState)
            {
                return trueState;
            }
            else
            {
                return PointState.None;
            }
        }
        public PlayResult MakeMove(Point point)
        {
            return _gameController.MakeMove(point);
        }
    }
}
