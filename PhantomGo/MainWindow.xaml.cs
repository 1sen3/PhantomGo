using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using System.Numerics;
using System;
using System.Threading.Tasks;

// 引入你的项目命名空间
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Agents;
using PhantomGo.AI; // 假设 RandomPlayer 在这里

namespace PhantomGo
{
    public sealed partial class MainWindow : Window
    {
        private GameController _gameController;
        private readonly IPlayerAgent _aiPlayer;
        private bool _isAiThinking = false;

        // 布局参数
        private const double BoardMargin = 0;
        private float _gridSpacing;
        private float _stoneRadius;
        private float _canvasRenderSize;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "PhantomGo on WinUI3";

            _aiPlayer = new RandomPlayer(); // 初始化AI
            StartNewGame();
        }

        private void StartNewGame()
        {
            _gameController = new GameController(9);
            _isAiThinking = false;
            // !! 核心改动 !!
            // 由于Canvas尺寸固定，我们只需计算一次布局
            CalculateLayout();

            UpdateUI();
        }

        #region UI Event Handlers

        private void GameBoardCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 如果是AI回合或游戏结束，则不响应点击
            if (_gameController.CurrentPlayer == Player.White ||
                _gameController.CurrentGameState != GameState.Playing || _isAiThinking)
            {
                return;
            }

            var point = e.GetCurrentPoint(GameBoardCanvas).Position;
            var logicalPoint = ScreenToLogical(new Vector2((float)point.X, (float)point.Y));

            if (logicalPoint.X > 0) // ScreenToLogical 返回 (0,0) 表示无效点击
            {
                var result = _gameController.MakeMove(logicalPoint);
                if (result.IsSuccess)
                {
                    UpdateUI();
                    // 立即触发AI回合
                    TriggerAiMove();
                }
                else
                {
                    // 可以弹窗提示错误，这里简单地在Debug输出
                    System.Diagnostics.Debug.WriteLine($"落子失败: {result.Message}");
                }
            }
        }

        private void PassButton_Click(object sender, RoutedEventArgs e)
        {
            if (_gameController.CurrentGameState != GameState.Playing || _isAiThinking) return;

            _gameController.Pass();
            UpdateUI();

            if (_gameController.CurrentPlayer == Player.White)
            {
                TriggerAiMove();
            }
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isAiThinking) return;

            // 悔棋会撤销AI和玩家的两步
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
                UpdateUI(); // 更新UI以显示AI正在思考（例如，禁用按钮）

                // AI思考和落子
                await Task.Delay(200); // 短暂延迟，让UI有响应感
                var aiMove = _aiPlayer.GenerateMove(_gameController);
                _gameController.MakeMove(aiMove);

                _isAiThinking = false;
                UpdateUI(); // AI落子后再次更新UI
            }
        }

        /// <summary>
        /// 核心UI更新函数
        /// </summary>
        private void UpdateUI()
        {
            // 更新状态文本
            CurrentPlayerText.Text = _gameController.CurrentPlayer.ToString();
            CurrentPlayerIndicator.Fill = new SolidColorBrush(_gameController.CurrentPlayer == Player.Black ? Colors.Black : Colors.White);
            BlackCapturesText.Text = _gameController.CapturedPointCount[Player.Black].ToString();
            WhiteCapturesText.Text = _gameController.CapturedPointCount[Player.White].ToString();

            // 根据AI是否在思考来决定按钮的可用性
            PassButton.IsEnabled = !_isAiThinking;
            UndoButton.IsEnabled = !_isAiThinking;

            // 请求Win2D重绘棋盘
            GameBoardCanvas.Invalidate();

            // 检查游戏是否结束
            if (_gameController.CurrentGameState != GameState.Playing)
            {
                DispatcherQueue.TryEnqueue(async () => await ShowGameOverDialog());
            }
        }

        private async Task ShowGameOverDialog()
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "游戏结束",
                CloseButtonText = "好的",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        #endregion

        #region Win2D Drawing & Coordinates

        private void CalculateLayout()
        {
            // CanvasControl会填充Border的内部区域。
            // Border尺寸是800x800，Padding是40，所以Canvas的尺寸是 800 - 40*2 = 720
            _canvasRenderSize = 800 - (float)(GameBoardCard.Padding.Left + GameBoardCard.Padding.Right);
            // 布局计算基于这个固定的渲染尺寸
            _gridSpacing = _canvasRenderSize / (_gameController.BoardSize - 1);
            _stoneRadius = _gridSpacing / 2 * 0.95f;
        }

        private void GameBoardCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_gridSpacing <= 0) return;
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Antialiased;
            // 1. 绘制棋盘线
            for (int i = 0; i < _gameController.BoardSize; i++)
            {
                float pos = i * _gridSpacing;
                ds.DrawLine(0, pos, _canvasRenderSize, pos, Colors.Black, 1.5f);
                ds.DrawLine(pos, 0, pos, _canvasRenderSize, Colors.Black, 1.5f);
            }

            // 2. 绘制星位 (9路棋盘通常是5个)
            var starPoints = new[] { new Point(3, 3), new Point(7, 3), new Point(5, 5), new Point(3, 7), new Point(7, 7) };
            foreach (var p in starPoints)
            {
                ds.FillCircle(LogicalToScreen(p), 4, Colors.Black);
            }

            // 3. 绘制棋子
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

        // 将棋盘逻辑坐标 (1-9) 转换为屏幕像素坐标
        // 这个方法现在也更简单了
        private Vector2 LogicalToScreen(Point logicalPoint)
        {
            return new Vector2(
                (logicalPoint.X - 1) * _gridSpacing,
                (logicalPoint.Y - 1) * _gridSpacing
            );
        }
        private Point ScreenToLogical(Vector2 screenPoint)
        {
            float halfGrid = _gridSpacing / 2;
            if (screenPoint.X < -halfGrid || screenPoint.X > _canvasRenderSize + halfGrid ||
                screenPoint.Y < -halfGrid || screenPoint.Y > _canvasRenderSize + halfGrid)
            {
                return new Point(0, 0); // 无效点击
            }
            int x = (int)Math.Round(screenPoint.X / _gridSpacing) + 1;
            int y = (int)Math.Round(screenPoint.Y / _gridSpacing) + 1;
            return new Point(x, y);
        }

        #endregion
    }
}
