using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Linq;

namespace PhantomGo.Test
{
    class TestModelShape
    {
        static void Main(string[] args)
        {
            string modelPath = @"D:\Project\ComputerGame\PhantomGo\PhantomGo\PhantomGo.Core\Assets\model2.onnx";

            using var session = new InferenceSession(modelPath);

            Console.WriteLine("=== 输入元数据 ===");
            foreach (var input in session.InputMetadata)
            {
                Console.WriteLine($"名称: {input.Key}");
                Console.WriteLine($"形状: [{string.Join(", ", input.Value.Dimensions)}]");
                Console.WriteLine($"类型: {input.Value.ElementType}");
            }

            Console.WriteLine("\n=== 输出元数据 ===");
            foreach (var output in session.OutputMetadata)
            {
                Console.WriteLine($"名称: {output.Key}");
                Console.WriteLine($"形状: [{string.Join(", ", output.Value.Dimensions)}]");
                Console.WriteLine($"类型: {output.Value.ElementType}");
            }

            // 测试一个空棋盘的推理
            Console.WriteLine("\n=== 测试推理（空棋盘，黑子先行） ===");
            var inputTensor = new DenseTensor<float>(new[] { 1, 9, 9, 17 });

            // 填充第 16 通道（黑子先行）
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    inputTensor[0, row, col, 16] = 1.0f;
                }
            }

            var inputName = session.InputMetadata.Keys.First();
            var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

            using var results = session.Run(inputs);

            var policyOutput = results.First(r => r.Name.ToLower().Contains("policy")).AsTensor<float>();
            var valueOutput = results.First(r => r.Name.ToLower().Contains("value")).AsTensor<float>();

            Console.WriteLine($"Policy 形状: [{string.Join(", ", policyOutput.Dimensions.ToArray())}]");
            Console.WriteLine($"Policy 长度: {policyOutput.Length}");
            Console.WriteLine($"Value: {valueOutput[0]}");

            // 找到概率最高的位置
            var policyArray = policyOutput.ToArray();
            int maxIdx = Array.IndexOf(policyArray, policyArray.Max());

            Console.WriteLine($"\n最高概率索引: {maxIdx}");
            Console.WriteLine($"最高概率值: {policyArray[maxIdx]}");

            // 转换为坐标
            if (maxIdx < 81)
            {
                int row = maxIdx / 9;
                int col = maxIdx % 9;
                Console.WriteLine($"对应坐标 (0-based): ({row}, {col})");

                // 转换为 GTP 坐标（从左下角开始）
                char colChar = (char)('A' + col);
                int rowGTP = 9 - row;
                Console.WriteLine($"GTP 坐标: {colChar}{rowGTP}");
            }
            else
            {
                Console.WriteLine("推荐 Pass");
            }

            Console.WriteLine("\n前10个最高概率位置:");
            var topIndices = policyArray
                .Select((prob, idx) => (prob, idx))
                .OrderByDescending(x => x.prob)
                .Take(10)
                .ToList();

            foreach (var (prob, idx) in topIndices)
            {
                if (idx < 81)
                {
                    int row = idx / 9;
                    int col = idx % 9;
                    char colChar = (char)('A' + col);
                    int rowGTP = 9 - row;
                    Console.WriteLine($"  #{idx}: {colChar}{rowGTP} - 概率 {prob:F4}");
                }
                else
                {
                    Console.WriteLine($"  #{idx}: Pass - 概率 {prob:F4}");
                }
            }
        }
    }
}
