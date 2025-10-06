﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using PhantomGo.Core.Agents;
using PhantomGo.Core.Helper;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;
using PhantomGo.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Services
{ 
    public class GameLogicService
    {
        private GameController _gameController;
        private readonly IPlayerAgent _blackAgent;
        private readonly IPlayerAgent _whiteAgent;
        private readonly Dictionary<Player, IPlayerAgent> _playerAgents;
        private bool _isAiThinking = false;

        // 事件通知UI更新
        public event Action<string> CurrentPlayerChanged;
        public event Action<int, int> CapturedCountChanged;
        public event Action<ScoreResult> ScoreChanged;
        public event Action<bool> AiThinkingChanged;
        public event Action<GameState> GameStateChanged;
        public event Action<Move> MoveAdded;
        public event Action GameRestarted;
        public event Action<string> GameEnded;
        public event Action BoardChanged;

        public GameController Game => _gameController;
        public bool IsAiThinking => _isAiThinking;
        // 游戏模式枚举
        public enum GameMode
        {
            HumanVsHuman,  // 双人对弈
            HumanVsAI,     // 人机对弈
            AIVsAI         // 双AI对弈
        }

        public GameMode CurrentGameMode { get; private set; }
        public bool IsPvE => CurrentGameMode == GameMode.HumanVsAI;
        public Player CurrentPlayer => _gameController.CurrentPlayer;
        public ObservableCollection<Move> MoveHistory { get; private set; }

        // 比赛信息
        private GameInfoService GameInfo => GameInfoService.Instance;
        public string BlackTeamName => "⚫ " + GameInfo.BlackTeamName;
        public string WhiteTeamName => "⚪ " + GameInfo.WhiteTeamName;
        private Dictionary<Player, string> TeamNames;
        public string CurrentTeamName => TeamNames[CurrentPlayer];
        public GameLogicService()
        {

            _whiteAgent = GameInfo.WhiteAgent;
            _blackAgent = GameInfo.BlackAgent;
            _playerAgents = new Dictionary<Player, IPlayerAgent>
            {
                { Player.Black, _blackAgent },
                { Player.White, _whiteAgent },
            };

            TeamNames = new Dictionary<Player, string>
            {
                {Player.Black, BlackTeamName },
                {Player.White, WhiteTeamName },
            };

            // 判断游戏模式
            if (_blackAgent is HumanPlayer && _whiteAgent is HumanPlayer)
            {
                CurrentGameMode = GameMode.HumanVsHuman;
            }
            else if ((_blackAgent is HumanPlayer && _whiteAgent is not HumanPlayer) ||
                     (_whiteAgent is HumanPlayer && _blackAgent is not HumanPlayer))
            {
                CurrentGameMode = GameMode.HumanVsAI;
            }
            else
            {
                CurrentGameMode = GameMode.AIVsAI;
            }

            MoveHistory = new ObservableCollection<Move>();
            StartNewGame();
        }
        public void StartNewGame()
        {
            if(_gameController != null)
            {
                _gameController.EndGame();
            }
            _gameController = new GameController(9);
            _isAiThinking = false;

            // 清空历史记录和 AI 记忆
            MoveHistory.Clear();
            _whiteAgent.Knowledge.Clear();
            _blackAgent.Knowledge.Clear();

            // 通知UI更新
            NotifyAllUIUpdates();
            GameRestarted?.Invoke();

            // 如果是双AI模式，启动第一步
            if (CurrentGameMode == GameMode.AIVsAI)
            {
                _ = ContinueGameFlow();
            }
        }

        // 游戏流程控制
        public async Task ContinueGameFlow()
        {
            // 检查游戏是否结束
            if (_gameController.CurrentGameState == GameState.Ended)
            {
                Debug.WriteLine($"[ContinueGameFlow] 游戏已结束");
                return;
            }

            var currentPlayer = _gameController.CurrentPlayer;
            var currentAgent = _playerAgents[currentPlayer];
            
            Debug.WriteLine($"[ContinueGameFlow] 当前玩家: {currentPlayer}, 是否人类: {currentAgent is HumanPlayer}");

            // 如果当前玩家是人类，等待人类操作
            if (currentAgent is HumanPlayer)
            {
                Debug.WriteLine($"[ContinueGameFlow] 等待人类玩家 {currentPlayer} 操作");
                return;
            }

            // 当前玩家是AI，执行AI操作
            Debug.WriteLine($"[ContinueGameFlow] 执行AI玩家 {currentPlayer} 动作");
            await ExecuteAiMove();

            // 递归继续游戏流程
            Debug.WriteLine($"[ContinueGameFlow] 递归继续游戏流程");
            await ContinueGameFlow();
        }
        // 处理人类玩家落子
        public async Task MakeHumanMove(Point point)
        {
            var currentPlayer = _gameController.CurrentPlayer;

            if (!IsHumanTurn(currentPlayer))
            {
                throw new Exception("当前不是你的回合");
            }
            var result = _gameController.MakeMove(point);

            // 向双方播报落子信息
            _blackAgent.ReceiveRefereeUpdate(currentPlayer, point, result);
            _whiteAgent.ReceiveRefereeUpdate(currentPlayer, point, result);

            if (result.IsSuccess)
            {
                var move = new Move
                {
                    Id = MoveHistory.Count + 1,
                    player = TeamNames[currentPlayer],
                    message = $"在 {point} 处落子",
                };
                MoveHistory.Insert(0, move);
                MoveAdded?.Invoke(move);

                NotifyAllUIUpdates();
            }
            else
            {
                throw new Exception("落子点不合法");
            }

            await ContinueGameFlow();
        }
        public async Task MakeUndo()
        {
            var currentPlayer = _gameController.CurrentPlayer;

            if (!IsHumanTurn(_gameController.CurrentPlayer))
            {
                throw new Exception("当前不是你的回合");
            }

            var result = _gameController.Undo(CurrentGameMode == GameMode.HumanVsAI);
            if(result.IsSuccess)
            {
                MoveHistory.RemoveAt(0);
                if (MoveHistory.Count > 0)
                {
                    MoveHistory.RemoveAt(0);
                }
                var move = new Move
                {
                    Id = MoveHistory.Count + 1,
                    player = TeamNames[currentPlayer],
                    message = "选择了悔棋",
                };
                MoveHistory.Insert(0, move);
                MoveAdded?.Invoke(move);

                NotifyAllUIUpdates();
            }

            await ContinueGameFlow();
        }
        public async Task MakePass()
        {
            var currentPlayer = _gameController.CurrentPlayer;

            if (!IsHumanTurn(currentPlayer))
            {
                throw new Exception("当前不是你的回合");
            }

            var result = _gameController.Pass();
            if(result.IsSuccess)
            {
                var move = new Move
                {
                    Id = MoveHistory.Count + 1,
                    player = TeamNames[currentPlayer],
                    message = "选择了虚着",
                };
                MoveHistory.Insert(0, move);
                MoveAdded?.Invoke(move);

                NotifyAllUIUpdates();
            }

            // 根据游戏模式继续游戏流程
            await ContinueGameFlow();
        }
        // 执行AI移动
        private async Task ExecuteAiMove()
        {
            var currentPlayer = _gameController.CurrentPlayer; 
            var currentAgent = _playerAgents[currentPlayer];
            
            Debug.WriteLine($"[ExecuteAiMove] 开始执行 {currentPlayer} 的AI动作");

            // 检查当前玩家是否为AI
            if (currentAgent is HumanPlayer)
            {
                Debug.WriteLine($"[ExecuteAiMove] 当前玩家 {currentPlayer} 是人类，跳过AI执行");
                return;
            }

            _isAiThinking = true;
            AiThinkingChanged?.Invoke(_isAiThinking);
            
            var gameView = new PhantomGoView(_gameController, currentPlayer);

            await Task.Delay(200);
            var point = currentAgent.GenerateMove();
            
            Debug.WriteLine($"[ExecuteAiMove] AI {currentPlayer} 选择落子位置: {point}");

            PlayResult result;

            // 检查是否是Pass操作
            if (point.Equals(Point.Pass()))
            {
                Debug.WriteLine($"[ExecuteAiMove] AI {currentPlayer} 选择Pass");
                result = _gameController.Pass();
            }
            else
            {
                result = _gameController.MakeMove(point);
            }
            _blackAgent.ReceiveRefereeUpdate(currentPlayer, point, result);
            _whiteAgent.ReceiveRefereeUpdate(currentPlayer, point, result);

            if (!result.IsSuccess)
            {
                Debug.WriteLine($"[ExecuteAiMove] AI {currentPlayer} 落子失败: {result.Message}，重试");
                // AI移动失败，递归重试
                _isAiThinking = false;
                AiThinkingChanged?.Invoke(_isAiThinking);
                await ExecuteAiMove();
                return;
            }
            
            Debug.WriteLine($"[ExecuteAiMove] AI {currentPlayer} 落子成功，当前玩家已切换为: {_gameController.CurrentPlayer}");
            
            // 成功移动，更新记录
            var move = new Move
            {
                Id = MoveHistory.Count + 1,
                player = TeamNames[currentPlayer],
                message = $"在 {point} 处落子",
            };
            MoveHistory.Insert(0, move);
            MoveAdded?.Invoke(move);

            _isAiThinking = false;
            AiThinkingChanged?.Invoke(_isAiThinking);
            NotifyAllUIUpdates();
            
            Debug.WriteLine($"[ExecuteAiMove] AI {currentPlayer} 动作完成");
        }
        private void NotifyAllUIUpdates()
        {
            CurrentPlayerChanged?.Invoke(CurrentTeamName);
            CapturedCountChanged?.Invoke(_gameController.CapturedPointCount[Player.Black], _gameController.CapturedPointCount[Player.White]);
            ScoreChanged?.Invoke(_gameController.GetScoreResult());
            GameStateChanged?.Invoke(_gameController.CurrentGameState);
            BoardChanged?.Invoke(); // 触发棋盘更新事件

            if(_gameController.CurrentGameState == GameState.Ended)
            {
                HandleGameEnded();
            }
        }
        private void HandleGameEnded()
        {
            var score = _gameController.GetScoreResult();
            if(GameInfo.IsEventMode)
            {
                var sgf = new SgfGenerator(
                    GameInfo.BlackTeamName,
                    GameInfo.WhiteTeamName,
                    score.BlackScore > score.WhiteScore ? "先手胜" : "后手胜",
                    $"{GameInfo.EventDateTime:yyyy.MM.dd HH：mm}" + " " + GameInfo.EventLocation,
                    GameInfo.EventName,
                    _gameController);
                sgf.SaveSgfToFile();
                Debug.Print($"尝试导出棋谱 {sgf.GenerateFileName()}");
            }

            StringBuilder result = new StringBuilder();
            result.Append($"最终得分：黑方 {score.BlackScore} vs 白方 {score.WhiteScore}，");
            if (score.BlackScore > score.WhiteScore)
            {
                result.Append("黑方 (先手) 胜利！");
            }
            else if (score.BlackScore < score.WhiteScore)
            {
                result.Append("白方 (后手) 胜利！");
            }
            else
            {
                result.Append("平局！");
            }

            GameEnded?.Invoke(result.ToString());
        }
        #region 辅助方法
        /// <summary>
        /// 获取指定玩家的知识状态
        /// </summary>
        /// <param name="player">玩家颜色</param>
        public PlayerKnowledge GetPlayerKnowledge(Player player)
        {
            return _playerAgents[player].Knowledge;
        }
        /// <summary>
        /// 获取当前游戏模式的描述
        /// </summary>
        public string GetGameModeDescription()
        {
            return CurrentGameMode switch
            {
                GameMode.HumanVsHuman => "双人对弈",
                GameMode.HumanVsAI => "人机对弈",
                GameMode.AIVsAI => "双AI对弈",
                _ => "未知模式"
            };
        }

        /// <summary>
        /// 检查当前是否为人类玩家的回合
        /// </summary>
        public bool IsCurrentPlayerHuman()
        {
            return _playerAgents[_gameController.CurrentPlayer] is HumanPlayer;
        }

        /// <summary>
        /// 检查是否可以进行人类操作（落子、虚着、悔棋）
        /// </summary>
        public bool CanHumanInteract()
        {
            return IsCurrentPlayerHuman() && 
                   _gameController.CurrentGameState == GameState.Playing && 
                   !_isAiThinking;
        }
        /// <summary>
        /// 当前是否为人类玩家的回合（保持向后兼容）
        /// </summary>
        public bool IsHumanTurn(Player player)
        {
            return _gameController.CurrentPlayer == player && _playerAgents[player] is HumanPlayer;
        }
        public bool IsGamePlaying()
        {
            return _gameController.CurrentGameState == GameState.Playing;
        }
        public bool IsGameEnded()
        {
            return _gameController.CurrentGameState == GameState.Ended;
        }
        public string GetCurrentTeamName()
        {
            return TeamNames[_gameController.CurrentPlayer];
        }
        public int GetCapturedCount(Player player)
        {
            return _gameController.CapturedPointCount[player];
        }
        public ScoreResult GetScoreResult()
        {
            return _gameController.GetScoreResult();
        }
        public PointState GetPointState(Point point)
        {
            return _gameController.GetPointState(point);
        }
        public PhantomGoView GetPhantomGoView(Player player)
        {
            return new PhantomGoView(_gameController, player);
        }
        #endregion
    }
}
