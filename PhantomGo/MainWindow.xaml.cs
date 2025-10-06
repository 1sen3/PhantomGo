using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PhantomGo.Core.Agents;
using PhantomGo.Core.Helper;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;
using PhantomGo.Models;
using PhantomGo.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;


namespace PhantomGo
{
    public sealed partial class MainWindow : Window
    {
        private GameLogicService _gameLogicService;
        private TimerService _timerService;
        private GameInfoService GameInfo => GameInfoService.Instance;
        private Player CurrentPlayer => _gameLogicService.CurrentPlayer;
        private ObservableCollection<Move> MoveHistory => _gameLogicService.MoveHistory;

        // Win2D 棋盘布局参数
        private float _gridSpacing;
        private float _stoneRadius;
        private float _canvasRenderSize;
        private int _boardSize => _gameLogicService.Game.BoardSize;

        private int _boardView; // 0: 裁判 1：黑方 2：白方
        private Point? _hoverPoint = null;

        // 为坐标标签定义的边距和字体格式
        private const float LabelMargin = 30;
        private CanvasTextFormat _labelTextFormat;

        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1130, 800));

            BoardSegmented.SelectedItem = RefereeSegment;
            StatusSegmented.SelectedItem = GameStatusSegment;

            _labelTextFormat = new CanvasTextFormat()
            {
                FontSize = 14,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            _boardView = 0;

            _gameLogicService = new GameLogicService();
            _timerService = new TimerService();

            CalculateLayout();
            UpdateBoard();

            // 绑定事件
            SubscribeToEvents();
            

            // 启动计时器
            _timerService.StartTimer();
        }
        private void SubscribeToEvents()
        {
            // 游戏逻辑事件
            _gameLogicService.CurrentPlayerChanged += UpdateCurrentPlayer;
            _gameLogicService.ScoreChanged += UpdateScores;
            _gameLogicService.CapturedCountChanged += UpdateCapturedCount;
            _gameLogicService.AiThinkingChanged += UpdateIsAiThinking;
            _gameLogicService.GameEnded += OnGameEnded;
            _gameLogicService.MoveAdded += OnMoveAdded;
            _gameLogicService.BoardChanged += UpdateBoard; // 订阅棋盘更新事件

            // 计时器事件
            _timerService.TimeUpdated += UpdateThinkingTime;
            
        }
        #region Event Handlers
        private async void OnGameEnded(string result)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "游戏结束",
                Content = result.ToString(),
                CloseButtonText = "好的",
                XamlRoot = this.Content.XamlRoot
            };
            dialog.CloseButtonClick += (s, e) => _gameLogicService.StartNewGame();
            await dialog.ShowAsync();
        }
        private void OnMoveAdded(Move move)
        {
            _timerService.RestartTimer();
        }
        #endregion

        #region UI Event Handlers

        private async void GameBoardCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 检查是否可以进行人类操作
            if (!_gameLogicService.CanHumanInteract())
            {
                return;
            }

            var point = e.GetCurrentPoint(GameBoardCanvas).Position;
            var logicalPoint = ScreenToLogical(new Vector2((float)point.X, (float)point.Y));

            if (logicalPoint.X > 0)
            {
                try
                {
                    await _gameLogicService.MakeHumanMove(logicalPoint);

                    UpdateBoard();
                } catch(Exception ex)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "落子失败",
                        Content = ex.Message,
                        CloseButtonText = "好的",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        private async void PassButton_Click(object sender, RoutedEventArgs e)
        {
           if (!_gameLogicService.CanHumanInteract()) return;

            try
            {
                await _gameLogicService.MakePass();
            } catch(Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "虚着失败",
                    Content = ex.Message,
                    CloseButtonText = "好的",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }

            UpdateBoard();
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_gameLogicService.CanHumanInteract()) return;
            try
            {
                await _gameLogicService.MakeUndo();
            } catch(Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "悔棋失败",
                    Content = ex.Message,
                    CloseButtonText = "好的",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }

            UpdateBoard();
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            _gameLogicService.StartNewGame();
            UpdateBoard();
        }

        #endregion

        #region Game Logic Integration

        private void UpdateBoard()
        {
            GameBoardCanvas.Invalidate();
        }
        private void UpdateCapturedCount(int blackCaptured, int whiteCaptured)
        {
            BlackCapturesText.Text = blackCaptured.ToString();
            WhiteCapturesText.Text = whiteCaptured.ToString();
        }
        private void UpdateScores(ScoreResult score)
        {
            BlackScore.Text = score.BlackScore.ToString();
            WhiteScore.Text = score.WhiteScore.ToString();
        }
        private void UpdateIsAiThinking(bool isThinking)
        {
            PassButton.IsEnabled = !isThinking;
            UndoButton.IsEnabled = !isThinking;
        }

        private void UpdateThinkingTime(int seconds)
        {
            ThinkingTime.Text = seconds.ToString();
        }
        private void UpdateCurrentPlayer(string playerName)
        {
            CurrentPlayerText.Text = playerName;
        }
        #endregion

        #region Win2D Drawing & Coordinates

        private void CalculateLayout()
        {
            _canvasRenderSize = 720;
            float boardAreaSize = _canvasRenderSize - (2 * LabelMargin);
            _gridSpacing = boardAreaSize / (_gameLogicService.Game.BoardSize);
            _stoneRadius = _gridSpacing * 0.35f;
        }

        private void GameBoardCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_gridSpacing <= 0) return;
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Antialiased;

            // 棋盘网格的绘制起点偏移，现在需要考虑标签边距
            float gridOffset = LabelMargin + (_gridSpacing / 2); // 网格线从半个格距开始
            float boardPixelSize = _gridSpacing * (_boardSize - 1);

            // 1. 绘制坐标标签
            for (int i = 0; i < _boardSize; i++)
            {
                // 绘制列号 (A-I)
                var colChar = Convert.ToChar('A' + i);
                float xPos = gridOffset + i * _gridSpacing;
                // 顶部
                ds.DrawText(colChar.ToString(), xPos, LabelMargin / 2, Colors.Gray, _labelTextFormat);
                // 底部
                ds.DrawText(colChar.ToString(), xPos, _canvasRenderSize - LabelMargin / 2, Colors.Gray, _labelTextFormat);

                // 绘制行号 (1-9)
                var rowNum = (i + 1).ToString(); // 围棋习惯，1在下方
                float yPos = gridOffset + i * _gridSpacing;
                // 左侧
                ds.DrawText(rowNum, LabelMargin / 2, yPos, Colors.Gray, _labelTextFormat);
                // 右侧
                ds.DrawText(rowNum, _canvasRenderSize - LabelMargin / 2, yPos, Colors.Gray, _labelTextFormat);
            }

            // 2. 绘制棋盘线 (使用新的gridOffset)
            for (int i = 0; i < _boardSize; i++)
            {
                float pos = gridOffset + i * _gridSpacing;
                ds.DrawLine(gridOffset, pos, gridOffset + boardPixelSize, pos, Colors.Black, 1.5f); // 横线
                ds.DrawLine(pos, gridOffset, pos, gridOffset + boardPixelSize, Colors.Black, 1.5f); // 竖线
            }

            // 3. 绘制星位 (LogicalToScreen已更新，无需改动此处逻辑)
            if (_boardSize == 9)
            {
                // 9路棋盘星位: (3,3), (7,3), (5,5), (3,7), (7,7)
                var starPoints = new[]
                {
                    new Point(3, 3), new Point(7, 3),
                    new Point(5, 5),
                    new Point(3, 7), new Point(7, 7)
                };
                foreach (var p in starPoints)
                {
                    ds.FillCircle(LogicalToScreen(p), 4, Colors.Black);
                }
            }

            // 4. 绘制棋子 (LogicalToScreen已更新，无需改动此处逻辑)
            Point_Draw(ds);

            // 5.检查是否有悬停点，并且当前是轮到人类玩家下棋
            if (_hoverPoint.HasValue && _gameLogicService.CanHumanInteract())
            {
                var center = LogicalToScreen(_hoverPoint.Value);
                var hoverColor = Color.FromArgb(32, 0, 0, 0);
                ds.FillCircle(center, _stoneRadius, hoverColor);
            }
        }

        private void Point_Draw(CanvasDrawingSession ds)
        {
            if(_boardView == 0) // 裁判视角
            {
                for (int x = 1; x <= _boardSize; x++)
                {
                    for (int y = 1; y <= _boardSize; y++)
                    {
                        var point = new Point(x, y);
                        var state = _gameLogicService.GetPointState(point);
                        if (state != PointState.None)
                        {
                            var center = LogicalToScreen(point);
                            var color = (state == PointState.black) ? Colors.Black : Colors.White;
                            ds.FillCircle(center, _stoneRadius, color);
                            ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
                        }
                    }
                }
            } else if(_boardView == 1) // 黑方视角
            {
                var knowledge = _gameLogicService.GetPlayerKnowledge(Player.Black);
                for (int x = 1; x <= _boardSize; x++)
                {
                    for (int y = 1; y <= _boardSize; y++)
                    {
                        var point = new Point(x, y);
                        var state = knowledge.GetMemoryState(point);
                        if (state == MemoryPointState.Self) // 己方棋子
                        {
                            var center = LogicalToScreen(point);
                            var color = Colors.Black;
                            ds.FillCircle(center, _stoneRadius, color);
                            ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
                        } else if(state == MemoryPointState.InferredOpponent) // 不可落子点
                        {
                            var center = LogicalToScreen(point);
                            var color = Colors.IndianRed;
                            ds.FillCircle(center, _stoneRadius, color);
                            ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
                        }
                    }
                }
            } else if(_boardView == 2) // 白方视角
            {
                var knowledge = _gameLogicService.GetPlayerKnowledge(Player.White);
                for (int x = 1; x <= _boardSize; x++)
                {
                    for (int y = 1; y <= _boardSize; y++)
                    {
                        var point = new Point(x, y);
                        var state = knowledge.GetMemoryState(point);
                        if (state == MemoryPointState.Self)
                        {
                            var center = LogicalToScreen(point);
                            var color = Colors.White;
                            ds.FillCircle(center, _stoneRadius, color);
                            ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
                        } else if(state == MemoryPointState.InferredOpponent)
                        {
                            var center = LogicalToScreen(point);
                            var color = Colors.MediumVioletRed;
                            ds.FillCircle(center, _stoneRadius, color);
                            ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
                        }
                    }
                }
            }

        }

        private Vector2 LogicalToScreen(Point logicalPoint)
        {
            // [修改] 转换时需要加上标签边距和半格偏移
            float gridOffset = LabelMargin + (_gridSpacing / 2);
            return new Vector2(
                gridOffset + (logicalPoint.X - 1) * _gridSpacing,
                gridOffset + (logicalPoint.Y - 1) * _gridSpacing
            );
        }

        private Point ScreenToLogical(Vector2 screenPoint)
        {
            // [修改] 转换时需要减去标签边距和半格偏移
            float gridOffset = LabelMargin + (_gridSpacing / 2);
            // 检查点击是否在棋盘网格区域内
            if (screenPoint.X < LabelMargin || screenPoint.X > _canvasRenderSize - LabelMargin ||
                screenPoint.Y < LabelMargin || screenPoint.Y > _canvasRenderSize - LabelMargin)
            {
                return new Point(0, 0); // 点击在边距上，无效
            }

            int x = (int)Math.Round((screenPoint.X - gridOffset) / _gridSpacing) + 1;
            int y = (int)Math.Round((screenPoint.Y - gridOffset) / _gridSpacing) + 1;

            // 检查边界
            if (x < 1 || x > _boardSize || y < 1 || y > _boardSize)
                return new Point(0, 0);

            return new Point(x, y);
        }

        private void GameBoardCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // 不是人类玩家回合，不显示悬停点
            if (!_gameLogicService.CanHumanInteract())
            {
                // 如果有悬停点，清除它
                if (_hoverPoint != null)
                {
                    _hoverPoint = null;
                    GameBoardCanvas.Invalidate();
                }
                return;
            }
            var screenPoint = e.GetCurrentPoint(GameBoardCanvas).Position;
            var logicalPoint = ScreenToLogical(new Vector2((float)screenPoint.X, (float)screenPoint.Y));
            Point? newHoverPoint = null;
            var view = _gameLogicService.GetPhantomGoView(CurrentPlayer);
            // 检查转换后的点是否在棋盘内，并且该位置是空的
            if (logicalPoint.X > 0 && view.GetPointState(logicalPoint) == PointState.None)
            {
                newHoverPoint = logicalPoint;
            }
            // 仅当悬停点发生变化时才更新并重绘，以提高性能
            if (_hoverPoint != newHoverPoint)
            {
                _hoverPoint = newHoverPoint;
                GameBoardCanvas.Invalidate(); // 请求重绘
            }
        }

        private void GameBoardCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // 当鼠标离开画布时，清除悬停点
            if (_hoverPoint != null)
            {
                _hoverPoint = null;
                GameBoardCanvas.Invalidate(); // 请求重绘以擦除提示
            }
        }

        #endregion

        private void StatusSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusSegmented.SelectedIndex == 0)
            {
                GameStatusCard.Visibility = Visibility.Visible;
                HistoryCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                GameStatusCard.Visibility = Visibility.Collapsed;
                HistoryCard.Visibility = Visibility.Visible;
            }
        }

        private void BoardSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch(BoardSegmented.SelectedIndex)
            {
                case 0:
                    _boardView = 1;
                    break;
                case 1:
                    _boardView = 0;
                    break;
                case 2:
                    _boardView = 2;
                    break;
            }
            GameBoardCanvas.Invalidate();
        }
    }
}
