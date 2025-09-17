using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using PhantomGo.AI;
using PhantomGo.Core.Agents;
using PhantomGo.Core.Helper;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using PhantomGo.Core.Views;

namespace PhantomGo.ConsoleApp
{
    class Program
    {
        private static GameController game = new GameController(9);
        static void Main(string[] args)
        {
            // 收集比赛信息
            string blackTeamName = "队1";
            string whiteTeamName = "队2";
            // 默认设置为和棋，在游戏结束时更新
            string winnerInfo = "和棋";
            string gameDateTimeAndLocation = $"{DateTime.Now:yyyy.MM.dd HH:mm} 本地";
            string eventName = "测试赛";

            IPlayerAgent aiPlayer = new RandomPlayer(game.BoardSize);
            IPlayerAgent humanPlayer = new RandomPlayer(game.BoardSize);
            // 决定谁执黑执白
            var playerAgents = new Dictionary<Player, IPlayerAgent>
            {
                { Player.Black, humanPlayer },
                { Player.White, aiPlayer },
            };
            var knowledgeBases = new Dictionary<Player, PlayerKnowledge>
            {
                { Player.Black, humanPlayer.Knowledge },
                { Player.White, aiPlayer.Knowledge },
            };
            while(game.CurrentGameState == GameState.Playing)
            {
                Console.Clear();
                PrintAllViews(humanPlayer.Knowledge, aiPlayer.Knowledge);
                PrintGameStatus();

                var currentPlayer = playerAgents[game.CurrentPlayer];
                var currentKnowledge = currentPlayer.Knowledge;

                var gameView = new PhantomGoView(game, game.CurrentPlayer);

                if(currentPlayer is HumanPlayer)
                {
                    Console.WriteLine("现在轮到你落子");
                } else
                {
                    Console.WriteLine($"现在轮到 AI 落子");
                }

                Point move = currentPlayer.GenerateMove(gameView, currentKnowledge);
                if(move.Equals(new Point(0, 0)))
                {
                    var result = game.Pass();
                    if(result.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("pass成功，输入回车继续...");
                        Console.ResetColor();
                        Console.ReadLine();
                    } else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"pass失败：{result.Message}，输入回车继续...");
                        Console.ResetColor();
                        Console.ReadLine();
                    }
                    continue;
                } else if(move.Equals(new Point(0, 1)))
                {
                    var result = game.Undo(true);
                    if (result.IsSuccess)
                    {
                        var history = game.GetMoveHistory();

                        playerAgents[Player.Black].RebuildKnowledgeFromHistory(history, Player.Black);
                        playerAgents[Player.White].RebuildKnowledgeFromHistory(history, Player.White);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("悔棋成功，输入回车继续...");
                        Console.ResetColor();
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"悔棋失败：{result.Message}，输入回车继续...");
                        Console.ResetColor();
                        Console.ReadLine();
                    }
                    continue;
                } else if(move.Equals(new Point(0, 2)))
                {
                    game.EndGame();
                } else
                {
                    var result = game.MakeMove(move);
                    currentPlayer.UpdateKnowledge(move, result);
                    // 如果发生提子，更新双方记忆
                    if (result.IsSuccess && result.CapturedPoints.Count > 0)
                    {
                        var capturedPlayer = game.CurrentPlayer;
                        var capturedKnowledge = knowledgeBases[capturedPlayer];
                        foreach (var point in result.CapturedPoints)
                        {
                            capturedKnowledge.RemoveState(point);
                        }
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"注意：{string.Join(", ", result.CapturedPoints)} 位置的棋子被提掉了");
                        Console.ResetColor();
                        Console.Write("输入回车继续...");
                        Console.ReadLine();
                        continue;
                    }
                    if (currentPlayer is HumanPlayer)
                    {
                        if (result.IsSuccess)
                        {
                            // 人类玩家的成功落子（无论是否提子）后暂停
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("落子成功，输入回车继续...");
                            Console.ResetColor();
                            Console.ReadLine();
                        }
                        else
                        {
                            // 人类玩家的失败落子后暂停
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"落子失败：{result.Message}，输入回车继续...");
                            Console.ResetColor();
                            Console.ReadLine();
                        }
                    }
                }
            }
            var score = game.GetScoreResult();
            Console.WriteLine($"最终得分：黑方 {score.BlackScore} vs 白方 {score.WhiteScore}");
            if (score.BlackScore > score.WhiteScore)
            {
                winnerInfo = "先手胜";
                Console.WriteLine("黑方 (先手) 胜利！");
            }
            else if (score.BlackScore < 
                score.WhiteScore)
            {
                winnerInfo = "后手胜";
                Console.WriteLine("白方 (后手) 胜利！");
            }
            else
            {
                winnerInfo = "和棋";
                Console.WriteLine("平局！");
            }
            // 生成并保存棋谱
            var sgfGenerator = new SgfGenerator(
                blackTeamName,
                whiteTeamName,
                winnerInfo,
                gameDateTimeAndLocation,
                eventName,
                game
            );
            sgfGenerator.SaveSgfToFile("GameRecords"); // 将棋谱保存在项目下的 GameRecords 文件夹中
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
        #region 辅助方法
        private static void PrintAllViews(PlayerKnowledge blackKnowledge, PlayerKnowledge whiteKnowledge)
        {
            int boardSize = game.BoardSize;
            string boardSpacing = "    ";
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            string titleBlack = " 黑子视角".PadRight(boardSize * 2);
            string titleWhite  = "白子视角".PadRight(boardSize * 2 - 1);
            string titleReferee = "裁判视角".PadRight(boardSize * 2 + 3);
            Console.WriteLine($"{titleBlack}{boardSpacing}{titleWhite}{boardSpacing}{titleReferee}");
            Console.ResetColor();
            for(int y = 1; y <= boardSize;++y)
            {
                var lineBuilder = new StringBuilder();
                lineBuilder.Append(y.ToString().PadLeft(2) + " ");
                for(int x = 1;x <= boardSize;++x)
                {
                    var state = blackKnowledge.GetMemoryState(new Point(x, y));
                    switch (state)
                    {
                        case MemoryPointState.Unknown:
                            lineBuilder.Append("+ ");
                            break;
                        case MemoryPointState.Self:
                            lineBuilder.Append("● ");
                            break;
                        case MemoryPointState.InferredOpponent:
                            lineBuilder.Append("X ");
                            break;
                    }

                }
                lineBuilder.Append(boardSpacing);
                lineBuilder.Append(y.ToString().PadLeft(2) + " ");
                for(int x = 1;x <= boardSize;++x)
                {
                    var state = whiteKnowledge.GetMemoryState(new Point(x, y));
                    switch (state)
                    {
                        case MemoryPointState.Unknown:
                            lineBuilder.Append("+ ");
                            break;
                        case MemoryPointState.Self:
                            lineBuilder.Append("○ ");
                            break;
                        case MemoryPointState.InferredOpponent:
                            lineBuilder.Append("X ");
                            break;
                    }
                }
                lineBuilder.Append(boardSpacing);
                lineBuilder.Append(y.ToString().PadLeft(2) + " ");
                for(int x = 1;x <= boardSize;++x)
                {
                    var state = game.GetPointState(new Point(x, y));
                    char symbol = state switch
                    {
                        PointState.black => '●',
                        PointState.white => '○',
                        _ => '+',
                    };
                    lineBuilder.Append(symbol + " ");
                }
                // 打印构建完成的整行
                Console.WriteLine(lineBuilder.ToString());
            }
            // 打印 X 轴坐标
            var xAxisBuilder = new StringBuilder();
            string singleAxis = "   " + string.Join(" ", Enumerable.Range(0, boardSize).Select(i => (char)('A' + i)));

            xAxisBuilder.Append(singleAxis);
            xAxisBuilder.Append(boardSpacing + " ");
            xAxisBuilder.Append(singleAxis);
            xAxisBuilder.Append(boardSpacing + " ");
            xAxisBuilder.Append(singleAxis);
            Console.WriteLine(xAxisBuilder.ToString());
            Console.WriteLine();
        } 
        /// <summary>
        /// 打印棋盘状态
        /// </summary>
        private static void PrintBoard()
        {
            Console.Write("  ");
            // 打印列坐标
            for (int x = 1; x <= game.BoardSize; ++x)
            {
                char symbol = (char)('A' + x - 1);
                Console.Write(symbol + " ");
            }
            Console.WriteLine();
            for (int y = 1; y <= game.BoardSize; ++y)
            {
                // 打印行坐标
                Console.Write(y + " ");
                for (int x = 1; x <= game.BoardSize; ++x)
                {
                    var state = game.GetPointState(new Point(x, y));
                    char symbol = state switch
                    {
                        PointState.black => 'B',
                        PointState.white => 'W',
                        _ => '+',
                    };
                    Console.Write(symbol + " ");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        /// <summary>
        /// 打印当前游戏状态
        /// </summary>
        private static void PrintGameStatus()
        {
            Console.WriteLine(new string('-', 20));
            Console.WriteLine($"当前玩家：{game.CurrentPlayer}");
            Console.WriteLine($"黑子提子数：{game.CapturedPointCount[Player.Black]}");
            Console.WriteLine($"白子提子数：{game.CapturedPointCount[Player.White]}");
            Console.WriteLine(new string('-', 20));
        }
        #endregion
    }
}