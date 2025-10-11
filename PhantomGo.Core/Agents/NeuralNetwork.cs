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
            const int numChannels = 17;
            const int boardSize = 9;
            var tensorData = new float[1 * boardSize * boardSize * numChannels];

            for (int y = 1; y <= boardSize; y++)
            {
                for (int x = 1; x <= boardSize; x++)
                {
                    int baseIndex = ((y - 1) * boardSize + (x - 1)) * numChannels;
                    var state = knowledge.GetMemoryState(new Point(x, y));

                    // Channel 0: 我方棋子位置
                    if (state == MemoryPointState.Self)
                    {
                        tensorData[baseIndex + 0] = 1f;
                    }
                    // Channel 1: 对方推测的棋子位置
                    else if (state == MemoryPointState.InferredOpponent)
                    {
                        tensorData[baseIndex + 1] = 1f;
                    }

                    // Channel 16: 轮到谁下棋 (全1代表黑方, 全0代表白方)
                    float colorToMove = (player == Player.Black) ? 1.0f : 0.0f;
                    tensorData[baseIndex + 16] = colorToMove;

                    // 其他通道保持为0
                }
            }

            return new DenseTensor<float>(tensorData, new[] { 1, boardSize, boardSize, numChannels });
        }

        public void Dispose() => _session?.Dispose();
    }
}