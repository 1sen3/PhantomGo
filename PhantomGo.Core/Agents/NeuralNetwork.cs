using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
            const int historyLength = 8;
            var tensorData = new DenseTensor<float>(new[] { 1, boardSize, boardSize, numChannels });

            int playerChannel = 0, opponentChannel = 1;
            
            for(int y = 1;y <= boardSize;++y)
            {
                for(int x = 1;x <= boardSize;++x)
                {
                    var point = new Point(x, y);
                    var state = knowledge.GetMemoryState(point);
                    if(state == MemoryPointState.Self)
                    {
                        tensorData[0, y - 1, x - 1, playerChannel] = 1.0f;
                    } else if(state == MemoryPointState.InferredOpponent)
                    {
                        tensorData[0, y - 1, x - 1, opponentChannel] = 1.0f;
                    }
                }
            }

            float playerToMove = (player == Player.Black) ? 1.0f : 0.0f;
            for(int y = 0;y < boardSize; ++y)
            {
                for(int x = 1;x < boardSize; ++x)
                {
                    tensorData[0, y, x, 16] = playerToMove;
                }
            }

            return tensorData;
        }

        public void Dispose() => _session?.Dispose();

        /// <summary>
        /// 将 DenseTensor 格式化打印到控制台
        /// </summary>
        private void PrintTensor(DenseTensor<float> tensor)
        {
            int channels = tensor.Dimensions[3];
            int height = tensor.Dimensions[1];
            int width = tensor.Dimensions[2];

            for (int c = 0; c < channels; c++)
            {
                Console.WriteLine($"--- Channel {c} ---");
                var sb = new StringBuilder();
                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        sb.Append(tensor[0, h, w, c] + " ");
                    }
                    sb.AppendLine();
                }
                Debug.WriteLine(sb.ToString());
            }
        }
    }
}