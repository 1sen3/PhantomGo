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
using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;


namespace PhantomGo
{
    public sealed partial class MainWindow : Window
    {
        private GameController _gameController;
        private readonly IPlayerAgent _aiPlayer;
        private bool _isAiThinking = false;

        private float _gridSpacing;
        private float _stoneRadius;
        private float _canvasRenderSize;

        // 为坐标标签定义的边距和字体格式
        private const float LabelMargin = 30;
        private CanvasTextFormat _labelTextFormat;

        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(2055, 1497));

            BoardSegmented.SelectedItem = RefereeSegment;
            StatusSegmented.SelectedItem = GameStatusSegment;

            _labelTextFormat = new CanvasTextFormat()
            {
                FontSize = 14,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            _aiPlayer = new SimpleAgentPlayer(9, Player.White);
            StartNewGame();
        }

        private void StartNewGame()
        {
            if (_gameController != null)
            {
                _gameController.EndGame();
            }
            _gameController = new GameController(9);
            _isAiThinking = false;

            // 计算布局
            CalculateLayout();

            UpdateUI();
        }

        #region UI Event Handlers

        private void GameBoardCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_gameController.CurrentPlayer == Player.White ||
                _gameController.CurrentGameState != GameState.Playing || _isAiThinking)
            {
                return;
            }

            var point = e.GetCurrentPoint(GameBoardCanvas).Position;
            var logicalPoint = ScreenToLogical(new Vector2((float)point.X, (float)point.Y));

            if (logicalPoint.X > 0)
            {
                var result = _gameController.MakeMove(logicalPoint);
                _aiPlayer.ReceiveRefereeUpdate(Player.Black, logicalPoint, result);
                Debug.Print($"玩家落子: {logicalPoint}, 结果: {result.IsSuccess}, 提子: {string.Join(",", result.CapturedPoints)}");
                if (result.IsSuccess)
                {
                    UpdateUI();
                    TriggerAiMove();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"落子失败: {result.Message}");
                }
            }
        }

        private void PassButton_Click(object sender, RoutedEventArgs e)
        {
            if (_gameController.CurrentGameState != GameState.Playing || _isAiThinking) return;

            var result = _gameController.Pass();
            if (result.IsSuccess)
            {
                _aiPlayer.ReceiveRefereeUpdate(Player.Black, new Point(0, 0), result);
            }

            UpdateUI();

            if (_gameController.CurrentPlayer == Player.White)
            {
                TriggerAiMove();
            }
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isAiThinking) return;

            var result = _gameController.Undo(true);
            if (!result.IsSuccess)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "悔棋失败",
                    Content = result.Message,
                    CloseButtonText = "好的",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            UpdateUI();
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            StartNewGame();
        }

        #endregion

        #region Game Logic Integration

        private async void TriggerAiMove()
        {
            if (_gameController.CurrentPlayer == Player.White && _gameController.CurrentGameState == GameState.Playing)
            {
                _isAiThinking = true;
                UpdateUI();

                var gameView = new PhantomGoView(_gameController, Player.White);

                await Task.Delay(200);
                var aiMove = _aiPlayer.GenerateMove(gameView, _aiPlayer.Knowledge);

                if(aiMove == Point.Pass())
                {
                    var passResult = _gameController.Pass();
                    _aiPlayer.ReceiveRefereeUpdate(Player.White, aiMove, passResult);
                    Debug.Print($"AI选择了Pass, 结果: {passResult.IsSuccess}");
                    _isAiThinking = false;
                    UpdateUI();
                    return;
                }

                var result = _gameController.MakeMove(aiMove);
                _aiPlayer.ReceiveRefereeUpdate(Player.White, aiMove, result);
                if (result.IsSuccess == false)
                {
                    TriggerAiMove();
                }

                Debug.Print($"AI落子: {aiMove}, 结果: {result.IsSuccess}, 提子: {string.Join(",", result.CapturedPoints)}");

                _isAiThinking = false;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            CurrentPlayerText.Text = _gameController.CurrentPlayer == Player.Black ? "⚫ HumanPlayer" : "⚪ AgentPlayer";
            BlackCapturesText.Text = _gameController.CapturedPointCount[Player.Black].ToString();
            WhiteCapturesText.Text = _gameController.CapturedPointCount[Player.White].ToString();

            PassButton.IsEnabled = !_isAiThinking;
            UndoButton.IsEnabled = !_isAiThinking;

            GameBoardCanvas.Invalidate();

            if (_gameController.CurrentGameState != GameState.Playing)
            {
                DispatcherQueue.TryEnqueue(async () => await ShowGameOverDialog());
            }
        }

        private async Task ShowGameOverDialog()
        {
            var score = _gameController.GetScoreResult();
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
            ContentDialog dialog = new ContentDialog
            {
                Title = "游戏结束",
                Content = result.ToString(),
                CloseButtonText = "好的",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        #endregion

        #region Win2D Drawing & Coordinates

        private void CalculateLayout()
        {
            // [修改] canvas尺寸由Border尺寸和Padding决定 (800 - 40*2 = 720)
            _canvasRenderSize = 720;

            // [修改] 用于绘制棋盘网格的区域大小，需要减去两边的标签边距
            float boardAreaSize = _canvasRenderSize - (2 * LabelMargin);

            // [修改] 网格间距基于新的棋盘区域大小计算
            _gridSpacing = boardAreaSize / (_gameController.BoardSize); // 注意：这里的除数改为了 BoardSize，使得线条刚好落在区域边缘
                                                                        // 如果希望棋盘线与标签间有更大空隙，可以改回 BoardSize + 1

            _stoneRadius = _gridSpacing * 0.35f; // 棋子半径略小于半格
        }

        private void GameBoardCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_gridSpacing <= 0) return;
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Antialiased;

            // [修改] 棋盘网格的绘制起点偏移，现在需要考虑标签边距
            float gridOffset = LabelMargin + (_gridSpacing / 2); // 网格线从半个格距开始
            float boardPixelSize = _gridSpacing * (_gameController.BoardSize - 1);

            // [新增] 1. 绘制坐标标签
            for (int i = 0; i < _gameController.BoardSize; i++)
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

            // [修改] 2. 绘制棋盘线 (使用新的gridOffset)
            for (int i = 0; i < _gameController.BoardSize; i++)
            {
                float pos = gridOffset + i * _gridSpacing;
                ds.DrawLine(gridOffset, pos, gridOffset + boardPixelSize, pos, Colors.Black, 1.5f); // 横线
                ds.DrawLine(pos, gridOffset, pos, gridOffset + boardPixelSize, Colors.Black, 1.5f); // 竖线
            }

            // [修改] 3. 绘制星位 (LogicalToScreen已更新，无需改动此处逻辑)
            if (_gameController.BoardSize == 9)
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

            // [修改] 4. 绘制棋子 (LogicalToScreen已更新，无需改动此处逻辑)
            for (int x = 1; x <= _gameController.BoardSize; x++)
            {
                for (int y = 1; y <= _gameController.BoardSize; y++)
                {
                    var point = new Point(x, y);
                    var state = _gameController.GetPointState(point);
                    if (state != PointState.None)
                    {
                        var center = LogicalToScreen(point);
                        var color = (state == PointState.black) ? Colors.Black : Colors.White;
                        ds.FillCircle(center, _stoneRadius, color);
                        ds.DrawCircle(center, _stoneRadius, Colors.Gray, 0.8f);
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
            if (x < 1 || x > _gameController.BoardSize || y < 1 || y > _gameController.BoardSize)
                return new Point(0, 0);

            return new Point(x, y);
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
    }
}
