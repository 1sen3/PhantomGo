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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Popups;


namespace PhantomGo
{
    public sealed partial class ContestWindow : Window, INotifyPropertyChanged
    {
        private TimerService _timerService;
        private GameInfoService GameInfo => GameInfoService.Instance;
        private ObservableCollection<Move> MoveHistory = new ObservableCollection<Move>();
        private Player _currentPlayer;
        private IPlayerAgent _agent;
        private string _teamName;
        private List<PlayerKnowledge> _knowledgeHistory = new List<PlayerKnowledge>();
        private bool _isThinking = false;
        private double _thinkingTime = 0;

        // Win2D 棋盘布局参数
        private float _gridSpacing;
        private float _stoneRadius;
        private float _canvasRenderSize;
        private int _boardSize = 9;

        // 为坐标标签定义的边距和字体格式
        private const float LabelMargin = 30;
        private CanvasTextFormat _labelTextFormat;

        public ContestWindow(bool isFirst)
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1080, 800));

            BoardSegmented_Initialize(isFirst);
            StatusSegmented.SelectedItem = GameStatusSegment;

            _labelTextFormat = new CanvasTextFormat()
            {
                FontSize = 14,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            
            _timerService = new TimerService();
            _currentPlayer = isFirst ? Player.Black : Player.White;
            _teamName = isFirst ? GameInfo.BlackTeamName : GameInfo.WhiteTeamName;
            _agent = isFirst ? GameInfo.BlackAgent : GameInfo.WhiteAgent;

            CalculateLayout();
            UpdateBoard();

            // 绑定事件
            SubscribeToEvents();
            
            // 启动计时器
            _timerService.StartTimer();

            SaveCurrentKnowledge();
        }

        private void InitializeLogic(bool isFirst)
        {
            BoardSegmented_Initialize(isFirst);
            _currentPlayer = isFirst ? Player.Black : Player.White;
            MoveHistory.Clear();
            _knowledgeHistory.Clear();
            _agent = new AIPlayer(_currentPlayer);
            UpdateBoard();
        }
        #region Event Handlers
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void SubscribeToEvents()
        {
            // 计时器事件
            _timerService.TimeUpdated += UpdateThinkingTime;

        }
        private async void OnGameEnded(string result)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "游戏结束",
                Content = result.ToString(),
                CloseButtonText = "好的",
                XamlRoot = this.Content.XamlRoot
            };
            // dialog.CloseButtonClick += (s, e) => _gameLogicService.StartNewGame();
            await dialog.ShowAsync();
        }
        private void OnMoveAdded(Move move)
        {
            _timerService.RestartTimer();
        }
        #endregion

        #region UI Event Handlers

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if(MoveHistory.Count <= 0)
            {
                await new ContentDialog
                {
                    Title = "悔棋失败",
                    Content = "历史记录为空，无法悔棋",
                    DefaultButton = ContentDialogButton.Primary,
                    PrimaryButtonText = "好",
                    XamlRoot = this.Content.XamlRoot,
                }.ShowAsync();
                return;
            }
            var undoComboBox = new ComboBox
            {
                ItemsSource = MoveHistory,
                DisplayMemberPath = "idAndMessage",
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var undoStackPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
            undoStackPanel.Children.Add(new TextBlock { Text = "请选择要撤回的落子:" });
            undoStackPanel.Children.Add(undoComboBox);
            var undoDialog = new ContentDialog
            {
                Title = "悔棋",
                Content = undoStackPanel,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var result =  await undoDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            { 
                int selectedIndexInComboBox = undoComboBox.SelectedIndex;

                int knowledgeIndex = _knowledgeHistory.Count - 2 - selectedIndexInComboBox;
                RestoreKnowledgeState(_knowledgeHistory[knowledgeIndex]);

                int statesToRemove = _knowledgeHistory.Count - (knowledgeIndex + 1);
                _knowledgeHistory.RemoveRange(knowledgeIndex + 1, statesToRemove);

                for (int i = 0; i <= selectedIndexInComboBox; i++)
                {
                    MoveHistory.RemoveAt(0);
                }
            }

            UpdateBoard();
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            var captureStackPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
            var captureText = new TextBlock { Text = "请输入被提掉子群中的一个子:" };
            var captureInput = new TextBox { PlaceholderText = "例:A1" };
            captureStackPanel.Children.Add(captureText);
            captureStackPanel.Children.Add(captureInput);

            var captureDialog = new ContentDialog
            {
                Title = "提子",
                Content = captureStackPanel,
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await captureDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string input = captureInput.Text.Trim();
                char[] delimeters = new char[] { ' ', ',', '，', '、' };

                bool parseSuccess = true;
                var points = new List<Point>();
                foreach (var coord in input.Split(delimeters))
                {
                    try
                    {
                        var point = Point.TransInputToPoint(coord);
                        points.Add(point);
                    }
                    catch (ArgumentException)
                    {
                        parseSuccess = false;
                        _ = new MessageDialog($"提子失败，无法识别坐标").ShowAsync();
                    }
                }

                if (parseSuccess)
                {
                    _agent.OnPointCaptured(points);
                    UpdateBoard();
                }
            }

            _knowledgeHistory[_knowledgeHistory.Count - 1] = _agent.Knowledge.Clone(); // 提子后覆盖最新的回放点知识
        }

        private async void MakeMoveButton_Click(object sender, RoutedEventArgs e)
        {
            IsThinkingTip.Visibility = Visibility.Visible;
            IsWatingTip.Visibility = Visibility.Collapsed;

            _timerService.RestartTimer();

            bool isMoveSuccess = false;

            try
            {
                _isThinking = true;
                OnThinkingStateChanged();
                var moveResult = await Task.Run(() => _agent.GenerateMove());
                var move = moveResult.Item2;
                var time = moveResult.Item1;
                _thinkingTime += time;

                _timerService.StopTimer();

                if (move.Equals(Point.Pass()))
                {
                    var PassDialog = new ContentDialog
                    {
                        Title = "Pass",
                        Content = "AI 选择了 PASS",
                        IsPrimaryButtonEnabled = false,
                        CloseButtonText = "好",
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = await PassDialog.ShowAsync();
                    isMoveSuccess = true;
                }
                else
                {
                    var JudgeDialog = new ContentDialog
                    {
                        Title = "落子",
                        Content = $"AI 选择在 {move} 处落子，是否合法？",
                        PrimaryButtonText = "是",
                        CloseButtonText = "否",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.Content.XamlRoot
                    };
                    var result = await JudgeDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        isMoveSuccess = true;
                        _agent.MakeMove(move);
                        MoveHistory.Insert(0, new Move { Id = MoveHistory.Count + 1, message = $"思考 {_thinkingTime:F2}s 后在 {move} 处落子" });
                        _thinkingTime = 0;
                        SaveCurrentKnowledge();
                        _agent.OnMoveSuccess();
                    }
                    else
                    {
                        _agent.OnMoveFailed();
                    }
                }

                UpdateBoard();
            }
            finally
            {
                _isThinking = false;
                IsWatingTip.Visibility = Visibility.Visible;
                IsThinkingTip.Visibility = Visibility.Collapsed;
                OnThinkingStateChanged();

                if (!isMoveSuccess)
                {
                    MakeMoveButton_Click(sender, e);
                }
            }
        }


        #endregion

        #region Game Logic Integration
        private void UpdateBoard()
        {
            GameBoardCanvas.Invalidate();
        }
        private void UpdateIsAiThinking(bool isThinking)
        {
            UndoButton.IsEnabled = !isThinking;
        }

        private void UpdateThinkingTime(int seconds)
        {
            ThinkingTime.Text = seconds.ToString();
        }
        #endregion

        #region Win2D Drawing & Coordinates

        private void CalculateLayout()
        {
            _canvasRenderSize = 720;
            float boardAreaSize = _canvasRenderSize - (2 * LabelMargin);
            _gridSpacing = boardAreaSize / (_boardSize);
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
                var rowNum = (9 - i).ToString(); // 围棋习惯，1在下方
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
        }

        private void Point_Draw(CanvasDrawingSession ds)
        {
            if(_currentPlayer == Player.Black) // 黑方视角
            {
                var knowledge = _agent.Knowledge;
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
            } else if(_currentPlayer == Player.White) // 白方视角
            {
                var knowledge = _agent.Knowledge;
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
            // 转换时需要加上标签边距和半格偏移
            float gridOffset = LabelMargin + (_gridSpacing / 2);
            return new Vector2(
                gridOffset + (logicalPoint.Col - 1) * _gridSpacing,
                gridOffset + (logicalPoint.Row - 1) * _gridSpacing
            );
        }
        #endregion

        private void BoardSegmented_Initialize(bool isFirst)
        {
            if (isFirst)
            {
                BlackSegment.Visibility = Visibility.Visible;
                WhiteSegment.Visibility = Visibility.Collapsed;
                BoardSegmented.SelectedItem = BlackSegment;
            }
            else
            {
                BlackSegment.Visibility = Visibility.Collapsed;
                WhiteSegment.Visibility = Visibility.Visible;
                BoardSegmented.SelectedItem = WhiteSegment;
            }
        }

        #region 回放辅助方法
        private void SaveCurrentKnowledge()
        {
            _knowledgeHistory.Add(_agent.Knowledge.Clone());
        }
        private void RestoreKnowledgeState(PlayerKnowledge stateToRestore)
        {
            _agent.Knowledge = stateToRestore.Clone();
        }
        public bool InvertBoolean(bool value) { return !value; }
        public void OnThinkingStateChanged()
        {
            if (_isThinking)
            {
                MakeMoveButton.IsEnabled = false;
                ThinkingProgressBar.Visibility = Visibility.Visible;
                ThinkingProgressBar.ShowPaused = false;
            } else
            {
                MakeMoveButton.IsEnabled = true;
                ThinkingProgressBar.Visibility = Visibility.Collapsed;
                ThinkingProgressBar.ShowPaused = true;
            }
        }
        #endregion

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            var isBlackDialog = new ContentDialog
            {
                Title = "重置",
                Content = "是否为先手？",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            var result = await isBlackDialog.ShowAsync();
            bool isFirst = result == ContentDialogResult.Primary;
            InitializeLogic(isFirst);
        }
    }
}
