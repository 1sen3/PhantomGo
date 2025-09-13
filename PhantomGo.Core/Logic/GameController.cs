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
    /// 记录一次落子操作
    /// </summary>
    /// <param name="player">落子玩家颜色</param>
    /// <param name="point">落子坐标</param>
    /// <param name="capturedStones">提子列表</param>
    public record MoveRecord(Player player, Point? point, IReadOnlyList<Point>? capturedStones);
    /// <summary>
    /// 游戏控制器，负责管理游戏流程、状态和玩家回合，与外部交互
    /// </summary>
    public class GameController
    {
        private GoBoard _board;
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
            _board = new GoBoard();
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
                return PlayResult.Failure(PlayResult.MoveError.GameOver, "落子失败，棋局已结束");
            }
            var playResult = _board.PlaceStone(point, CurrentPlayer);
            if(!playResult.IsSuccess)
            {
                return playResult;
            }
            // 移动有效
            // 更新提子数
            if(playResult.CapturedPoints.Count > 0)
            {
                CapturedPointCount[CurrentPlayer] += playResult.CapturedPoints.Count;
            }
            // 重置连续 pass 计数
            _consecutivePasses = 0;
            // 将移动计入历史
            _moveHistory.Add(new MoveRecord(CurrentPlayer, point, playResult.CapturedPoints));
            // 切换玩家
            SwitchPlayer();
            return PlayResult.Success(playResult.CapturedPoints, $"落子成功，提子数为{playResult.CapturedPoints.Count}");
        }
        /// <summary>
        /// 执行一次 pass 操作
        /// </summary>
        /// <returns>一个 PlayResult 对象，表示 pass 操作的结果</returns>
        public PlayResult Pass()
        {
            if (CurrentGameState.Equals(GameState.Ended))
            {
                return PlayResult.Failure(PlayResult.MoveError.GameOver, "棋局已结束");
            }
            _consecutivePasses++;
            _moveHistory.Add(new MoveRecord(CurrentPlayer, null, null));
            // 双方都 pass 时，结束游戏
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
        public bool Undo()
        {
            if(_moveHistory.Count == 0)
            {
                return false;
            }
            // 找到倒数第二个移动历史 
            var moveHistoryToReplay = _moveHistory.Take(_moveHistory.Count - 1).ToList();
            // 重置棋局
            ResetGame();
            // 回溯倒数第二个移动历史的所有操作
            foreach(var move in moveHistoryToReplay)
            {
                _replayMove(move);
            }
            if(CurrentGameState.Equals(GameState.Ended))
            {
                CurrentGameState = GameState.Playing;
            }
            return true;
        }
        #region 辅助方法
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
        /// <summary>
        /// 重放操作
        /// </summary>
        /// <param name="move"></param>
        private void _replayMove(MoveRecord move)
        {
            if(move.point.HasValue)
            {
                var playResult = _board.PlaceStone(move.point.Value, move.player);
                if(playResult.CapturedPoints.Count > 0)
                {
                    CapturedPointCount[move.player] += playResult.CapturedPoints.Count;
                }
                _consecutivePasses = 0;
            } else
            {
                _consecutivePasses++;
                if(_consecutivePasses >= 2)
                {
                    EndGame();
                }
            }
            _moveHistory.Add(move);
            SwitchPlayer();
        }
        public int getHistoryCount()
        {
            return _moveHistory.Count;
        }
        /// <summary>
        /// 结束游戏
        /// </summary>
        private void EndGame()
        {
            CurrentGameState = GameState.Ended;
        }
        /// <summary>
        /// 重置棋局
        /// </summary>
        private void ResetGame()
        {
            _board = new GoBoard();
            _moveHistory.Clear();
            CurrentPlayer = Player.Black;
            CurrentGameState = GameState.Playing;
            CapturedPointCount[Player.White] = 0;
            CapturedPointCount[Player.Black] = 0;
            _consecutivePasses = 0;
        }
        #endregion
    }
}
