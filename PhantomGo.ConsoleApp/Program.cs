using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Numerics;

namespace PhantomGo.ConsoleApp
{
    class Program
    {
        private static GameController _gameController;
        static void Main(string[] args)
        {
            _gameController = new GameController(9);
            while(_gameController.CurrentGameState == GameState.Playing)
            {
                Console.Clear();
                PrintBoard();
                PrintGameStatus();
                Console.Write("请输入落子位置（x, y)、'pass'、'undo'或'quit'：");
                GetPlayerInput();
            }
            // 棋局结束后显示结算信息
            Console.Clear();
            PrintBoard();
            Console.WriteLine("棋局结束");
            Console.WriteLine($"黑子提子数：{_gameController.CapturedPointCount[Player.Black]}");
            Console.WriteLine($"白子提子数：{_gameController.CapturedPointCount[Player.White]}");
        }
        /// <summary>
        /// 打印棋盘状态
        /// </summary>
        private static void PrintBoard()
        {
            Console.Write("  ");
            // 打印列坐标
            for(int x = 1;x <= _gameController.BoardSize;++x)
            {
                Console.Write(x + " ");
            }
            Console.WriteLine();
            for(int y = 1;y <= _gameController.BoardSize;++y)
            {
                // 打印行坐标
                Console.Write(y + " ");
                for(int x = 1;x <= _gameController.BoardSize; ++x)
                {
                    var state = _gameController.GetPointState(new Point(x,y));
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
            Console.WriteLine() ;
        }
        /// <summary>
        /// 打印当前游戏状态
        /// </summary>
        private static void PrintGameStatus()
        {
            Console.WriteLine(new string('-', 20));
            Console.WriteLine($"当前玩家：{_gameController.CurrentPlayer}");
            Console.WriteLine($"黑子提子数：{_gameController.CapturedPointCount[Player.Black]}");
            Console.WriteLine($"白子提子数：{_gameController.CapturedPointCount[Player.White]}");
            Console.WriteLine(new string('-', 20));
        }
        /// <summary>
        /// 获取并处理玩家输入
        /// </summary>
        private static void GetPlayerInput()
        {
            string input = Console.ReadLine()?.ToLower().Trim();
            if (string.IsNullOrEmpty(input)) return;
            if("quit".Equals(input))
            {
                Environment.Exit(0);
            } else if("pass".Equals(input))
            {
                var result = _gameController.Pass();
                Console.WriteLine(result.Message);
                if(_gameController.CurrentGameState != GameState.Ended)
                {
                    Console.Write("输入回车以继续...");
                    Console.ReadLine();
                }
            } else if("undo".Equals(input))
            {
                
                if(_gameController.Undo())
                {
                    Console.Clear();
                    PrintBoard();
                    PrintGameStatus();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("悔棋成功");
                    Console.ResetColor();
                } else
                {
                    Console.Clear();
                    PrintBoard();
                    PrintGameStatus();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("悔棋失败");
                    Console.ResetColor();
                }
                Console.Write("输入回车以继续...");
                Console.ReadLine();
            } else
            {
                var parts = input.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y)) {
                    var result = _gameController.MakeMove(new Point(x, y));
                    if (!result.IsSuccess)
                    {
                        // 如果移动失败，打印错误信息
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"落子失败，失败原因为：{result.Message}");
                        Console.ResetColor();
                        Console.Write("输入回车重试...");
                        Console.ReadLine();
                    }
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("输入不合法，输入回车重试...");
                    Console.ResetColor();
                    Console.ReadLine();
                }
            }
        }
    }
}