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
            var inputTensor = KnowledgeToTensor(knowledge, player);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

            using var results = _session.Run(inputs);

            var policyTensor = results.First(r => r.Name == _policyOutputName).AsTensor<float>();
            var valueTensor = results.First(r => r.Name == _valueOutputName).AsTensor<float>();

            return (policyTensor.ToArray(), valueTensor[0]);
        }

        /// <summary>
        /// 使用棋盘进行预测
        /// </summary>
        public (float[] Policy, float Value) Predict(GoBoard board, Player player)
        {
            var inputTensor = BoardToTensor(board, player);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

            using var results = _session.Run(inputs);

            var policyTensor = results.First(r => r.Name == _policyOutputName).AsTensor<float>();
            var valueTensor = results.First(r => r.Name == _valueOutputName).AsTensor<float>();


            return (policyTensor.ToArray(), valueTensor[0]);
        }

        private DenseTensor<float> KnowledgeToTensor(PlayerKnowledge knowledge, Player player)
        {
            const int numChannels = 17;
            const int boardSize = 9;
            const int historyLength = 8;
            var tensorData = new DenseTensor<float>(new[] { 1, boardSize, boardSize, numChannels });

            int playerChannel = 0, opponentChannel = 1, koChannel = 2;

            for(int row = 1; row <= boardSize; ++row)
            {
                for(int col = 1; col <= boardSize; ++col)
                {
                    var point = new Point(row, col);  // Point(row, col)
                    var state = knowledge.GetMemoryState(point);
                    if (state == MemoryPointState.Self)
                    {
                        tensorData[0, row - 1, col - 1, playerChannel] = 1.0f;
                    }
                    else if (state == MemoryPointState.InferredOpponent)
                    {
                        tensorData[0, row - 1, col - 1, opponentChannel] = 1.0f;
                    }
                    else if (state == MemoryPointState.KoBlocked) {
                        tensorData[0, row - 1, col - 1, koChannel] = 1.0f;
                    }
                }
            }

            float playerToMove = (player == Player.Black) ? 1.0f : 0.0f;
            for(int row = 0; row < boardSize; ++row)
            {
                for(int col = 0; col < boardSize; ++col)
                {
                    tensorData[0, row, col, 16] = playerToMove;
                }
            }

            return tensorData;
        }

        private DenseTensor<float> BoardToTensor(GoBoard board, Player player)
        {
            const int numChannels = 17;
            const int boardSize = 9;
            const int historyLength = 8;
            var tensorData = new DenseTensor<float>(new[] { 1, boardSize, boardSize, numChannels });

            // 获取历史棋盘状态
            var history = board.GetBoardHistory(historyLength);

            // 填充16个历史通道
            for (int t = 0; t < historyLength; t++)
            {
                var historicalBoard = history[t];

                for (int row = 1; row <= boardSize; row++)
                {
                    for (int col = 1; col <= boardSize; col++)
                    {
                        var state = historicalBoard[row, col];

                        // 偶数通道：当前行棋方的棋子
                        if ((player == Player.Black && state == PointState.black) ||
                            (player == Player.White && state == PointState.white))
                        {
                            tensorData[0, row - 1, col - 1, t * 2] = 1.0f;
                        }

                        // 奇数通道：对手的棋子
                        if ((player == Player.Black && state == PointState.white) ||
                            (player == Player.White && state == PointState.black))
                        {
                            tensorData[0, row - 1, col - 1, t * 2 + 1] = 1.0f;
                        }
                    }
                }
            }

            // 通道 16：当前行棋方颜色
            float playerToMove = (player == Player.Black) ? 1.0f : 0.0f;
            for (int row = 0; row < boardSize; row++)
            {
                for (int col = 0; col < boardSize; col++)
                {
                    tensorData[0, row, col, 16] = playerToMove;
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