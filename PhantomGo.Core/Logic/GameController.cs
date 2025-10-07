using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PhantomGo.Core.Models;

namespace PhantomGo.Core.Logic
{
    /// <summary>
    /// 表示当前棋局的状态
    /// </summary>
    public enum GameState
    {
        Playing,
        Ended,
    }
    /// <summary>
    /// 游戏控制器，负责管理游戏流程、状态和玩家回合，与外部交互
    /// </summary>
    public class GameController
    {
        private GoBoard _board;
        private readonly List<GameStateRecord> _history; // 操作历史（棋谱）
        private readonly List<MoveRecord> _moveHistory;
        private int _consecutivePasses;
        /// <summary>  
        /// 获取当前棋局状态
        /// </summary>
        public GameState CurrentGameState { get;private set; }
        /// <summary>
        /// 获取棋盘大小
        /// </summary>
        public int BoardSize => _board.Size;
        /// <summary>
        /// 获取当前玩家
        /// </summary>
        public Player CurrentPlayer { get; private set; }
        /// <summary>
        /// 获取一个字典，包含双方玩家提子的数量
        /// </summary>
        public Dictionary<Player, int> CapturedPointCount { get; }
        /// <summary>
        /// 初始化游戏控制器
        /// </summary>
        /// <param name="boardSize"></param>
        public GameController(int boardSize)
        {
            _board = new GoBoard(boardSize);
            _history = new List<GameStateRecord>();
            _moveHistory = new List<MoveRecord>();
            CurrentPlayer = Player.Black; // 黑子先行
            CurrentGameState = GameState.Playing;
            CapturedPointCount = new Dictionary<Player, int>
            {
                { Player.Black, 0 },
                { Player.White, 0 },
            };
            _consecutivePasses = 0;
        }
        /// <summary>
        /// 执行一次落子操作
        /// </summary>
        /// <param name="point">落子坐标</param>
        /// <returns>一个 PlayResult 对象，表示落子结果</returns>
        public PlayResult MakeMove(Point point)
        {
            if(CurrentGameState.Equals(GameState.Ended))
            {
                return PlayResult.Failure("该落子位置不合法");
            }

            // 落子前将棋局保存到历史记录中
            SaveCurrentStateToHistory();

            var playResult = _board.PlaceStone(point, CurrentPlayer);
            _moveHistory.Add(new MoveRecord(CurrentPlayer, point, playResult));
            if(!playResult.IsSuccess)
            {
                // 落子失败时直接返回结果，并将保存的记录删除
                _history.RemoveAt(_history.Count - 1);
                _moveHistory.RemoveAt(_moveHistory.Count - 1);
                return playResult;
            }
            // 移动有效, 更新状态
            if (playResult.CapturedPoints?.Count > 0)
            {
                CapturedPointCount[CurrentPlayer] += playResult.CapturedPoints.Count;
            }
            // 切换玩家
            SwitchPlayer();
            return PlayResult.Success(playResult.CapturedPoints, $"落子成功，提子数为{playResult.CapturedPoints?.Count}");
        }
        /// <summary>
        /// 执行一次 pass 操作
        /// </summary>
        /// <returns>一个 PlayResult 对象，表示 pass 操作的结果</returns>
        public PlayResult Pass()
        {
            if (CurrentGameState.Equals(GameState.Ended))
            {
                return PlayResult.Failure("棋局已结束");
            }

            SaveCurrentStateToHistory();
            _consecutivePasses++;
            if(_consecutivePasses >= 2)
            {
                EndGame();
                return PlayResult.Success(Array.Empty<Point>(), "双方均pass，游戏结束");
            }

            // 切换玩家
            SwitchPlayer();
            return PlayResult.Success(Array.Empty<Point>(), "pass成功");
        }
        /// <summary>
        /// 执行一次悔棋操作
        /// </summary>
        public PlayResult Undo(bool isPvE)
        {
            int step = isPvE ? 2 : 1;
            if (_history.Count < step)
            {
                return PlayResult.Failure("历史记录不足，悔棋失败");
            }

            int targetStateIndex = _history.Count - step;
            var stateToRestore = _history[targetStateIndex];

            for (int i = 0;i < step;++i)
            {
                if (_history.Count > 0)
                {
                    _history.RemoveAt(_history.Count - 1);
                }
                if (_moveHistory.Count > 0)
                {
                    _moveHistory.RemoveAt(_moveHistory.Count - 1);
                }
            }
            // 恢复到目标状态
            RestoreStateFromHistory(stateToRestore);

            // 如果游戏之前已结束，现在悔棋了，状态应该回到Playing
            if (CurrentGameState == GameState.Ended)
            {
                CurrentGameState = GameState.Playing;
            }

            // 如果悔棋后历史记录为空，则完全重置
            if (_history.Count == 0)
            {
                ResetGame();
            }

            return PlayResult.Success(Array.Empty<Point>(), "悔棋成功");
        }
        #region 辅助方法
        public GoBoard GetBoard()
        {
            return this._board.Clone();
        }
        /// <summary>
        /// 获取动作历史记录
        /// </summary>
        public List<MoveRecord> GetMoveHistory()
        {
            return new List<MoveRecord>(_moveHistory);
        }
        /// <summary>
        /// 获取最终得分结果
        /// </summary>
        public ScoreResult GetScoreResult()
        {
            var scoreCalculator = new ScoreCalculator(_board);
            return scoreCalculator.CalculateScores();
        }
        /// <summary>
        /// 将当前棋局保存到历史记录中
        /// </summary>
        public void SaveCurrentStateToHistory()
        {
            var record = new GameStateRecord(_board, CurrentPlayer, CapturedPointCount, _consecutivePasses);
            _history.Add(record);
        }
        /// <summary>
        /// 从历史记录中恢复棋局
        /// </summary>
        public void RestoreStateFromHistory(GameStateRecord record)
        {
            _board = record.Board.Clone();
            CurrentPlayer = record.CurrentPlayer;
            CapturedPointCount[Player.Black] = record.CapturedPointCounts[Player.Black];
            CapturedPointCount[Player.White] = record.CapturedPointCounts[Player.White];
            _consecutivePasses = record.ConsecutivePasses;
        }
        public bool IsValidMove(Point point, Player player)
        {
            return _board.IsValidMove(point, player);
        }
        /// <summary>
        /// 获取棋盘上指定坐标的状态
        /// </summary>
        public PointState GetPointState(Point point)
        {
            return _board.GetPointState(point);
        }
        /// <summary>
        /// 切换玩家
        /// </summary>
        private void SwitchPlayer()
        {
            CurrentPlayer = PlayerHelper.GetOpponent(CurrentPlayer);
        }
        public int getHistoryCount()
        {
            return _history.Count;
        }
        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame()
        {
            CurrentGameState = GameState.Ended;
        }
        /// <summary>
        /// 重置棋局
        /// </summary>
        private void ResetGame()
        {
            _board = new GoBoard();
            _history.Clear();
            CurrentPlayer = Player.Black;
            CurrentGameState = GameState.Playing;
            CapturedPointCount[Player.White] = 0;
            CapturedPointCount[Player.Black] = 0;
            _consecutivePasses = 0;
        }
        #endregion
    }
}
