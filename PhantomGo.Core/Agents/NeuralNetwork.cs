using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhantomGo.Core.Agents
{
    public class NeuralNetwork : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly string _policyOutputName;
        private readonly string _valueOutputName;
        private const int BoardSize = 9;
        private const int NumChannels = 17;

        public NeuralNetwork(string modelPath)
        {
            try
            {
                _session = new InferenceSession(modelPath);
                _inputName = _session.InputMetadata.Keys.First();
                _policyOutputName = _session.OutputMetadata.Keys.First(k => k.ToLower().Contains("policy"));
                _valueOutputName = _session.OutputMetadata.Keys.First(k => k.ToLower().Contains("value"));
            }
            catch (Exception e)
            {
                throw new Exception($"加载ONNX模型失败: {modelPath}。错误: {e.Message}");
            }
        }

        public (float[] Policy, float Value) Predict(PlayerKnowledge knowledge, Player player)
        {
            var inputTensor = BoardToTensor(knowledge, player);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

            using var results = _session.Run(inputs);

            var policyTensor = results.First(r => r.Name == _policyOutputName).AsTensor<float>();
            var valueTensor = results.First(r => r.Name == _valueOutputName).AsTensor<float>();

            return (policyTensor.ToArray(), valueTensor[0]);
        }

        private DenseTensor<float> BoardToTensor(PlayerKnowledge knowledge, Player player)
        {
            // 按照模型期望的 NHWC 格式创建数组
            var tensorData = new float[1 * BoardSize * BoardSize * NumChannels];
            var opponent = player.GetOpponent();

            // 遍历棋盘上的每一个点 (x, y)
            for (int y = 1; y <= BoardSize; y++)
            {
                for (int x = 1; x <= BoardSize; x++)
                {
                    // --- 核心修改：为每个点填充所有通道的数据 ---
                    int baseIndex = ((y - 1) * BoardSize + (x - 1)) * NumChannels;

                    // --- 填充历史通道 (0-15) ---
                    for (int t = 0; t < PlayerKnowledge.HistoryLength; t++)
                    {
                        var historicState = knowledge.GetHistoryState(t);
                        if (historicState != null)
                        {
                            var state = historicState[x, y];
                            // 我方历史通道
                            if (state == MemoryPointState.Self)
                                tensorData[baseIndex + t] = 1f;

                            // 对方历史通道
                            if (state == MemoryPointState.InferredOpponent)
                                tensorData[baseIndex + t + PlayerKnowledge.HistoryLength] = 1f;
                        }
                    }

                    // --- 填充最后一个颜色通道 (16) ---
                    float colorToMove = (player == Player.Black) ? 1.0f : 0.0f;
                    tensorData[baseIndex + 16] = colorToMove;
                }
            }

            // 使用正确的维度顺序创建张量: [batch, height, width, channels]
            return new DenseTensor<float>(tensorData, new[] { 1, BoardSize, BoardSize, NumChannels });
        }

        public void Dispose() => _session?.Dispose();
    }
}