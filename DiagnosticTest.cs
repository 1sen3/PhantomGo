using PhantomGo.Core.Agents;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Linq;

namespace PhantomGo.Diagnostic
{
    class DiagnosticTest
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 诊断测试：空棋盘第一手 ===\n");

            string modelPath = @"D:\Project\ComputerGame\PhantomGo\PhantomGo\PhantomGo.Core\Assets\model2.onnx";
            using var network = new NeuralNetwork(modelPath);

            // 创建一个完全空的棋盘
            var emptyBoard = new GoBoard();

            Console.WriteLine("测试 1: 空棋盘，黑子先行");
            var (policy1, value1) = network.Predict(emptyBoard, Player.Black);

            Console.WriteLine($"Value 评估: {value1:F4}");
            Console.WriteLine($"Policy 长度: {policy1.Length}");

            // 找到前 10 个最高概率位置
            var topMoves = policy1
                .Select((prob, idx) => (prob, idx))
                .OrderByDescending(x => x.prob)
                .Take(10)
                .ToList();

            Console.WriteLine("\n前 10 个最高概率位置:");
            foreach (var (prob, idx) in topMoves)
            {
                if (idx < 81)
                {
                    int row = idx / 9 + 1;  // 1-based
                    int col = idx % 9 + 1;  // 1-based
                    var point = new Point(row, col);
                    Console.WriteLine($"  索引 {idx:D2} -> Point({row},{col}) -> {point} - 概率 {prob:F6}");
                }
                else
                {
                    Console.WriteLine($"  索引 {idx:D2} -> Pass - 概率 {prob:F6}");
                }
            }

            // 对比：如果天元（E5）应该是什么索引？
            Console.WriteLine("\n=== 坐标对照表 ===");
            Console.WriteLine("天元 E5 应该对应:");
            Console.WriteLine("  Point(5, 5) -> 索引 " + PointToIndex(5, 5));
            Console.WriteLine("  概率: " + policy1[PointToIndex(5, 5)]);

            Console.WriteLine("\nF5 对应:");
            Console.WriteLine("  Point(5, 6) -> 索引 " + PointToIndex(5, 6));
            Console.WriteLine("  概率: " + policy1[PointToIndex(5, 6)]);

            Console.WriteLine("\n=== 棋盘索引可视化 ===");
            Console.WriteLine("  A B C D E F G H I");
            for (int row = 1; row <= 9; row++)
            {
                Console.Write($"{10 - row} ");
                for (int col = 1; col <= 9; col++)
                {
                    int idx = PointToIndex(row, col);
                    Console.Write($"{idx:D2} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine("\n按回车键退出...");
            Console.ReadLine();
        }

        static int PointToIndex(int row, int col)
        {
            return (row - 1) * 9 + (col - 1);
        }
    }
}
