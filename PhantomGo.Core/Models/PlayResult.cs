using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示一次操作的结果
    /// </summary>
    public class PlayResult
    {
        /// <summary>
        /// 表示操作是否成功
        /// </summary>
        public bool IsSuccess { get; }
        /// <summary>
        /// 如果落子成功且可提子，获取被提的棋子列表。
        /// </summary>
        public IReadOnlyList<Point>? CapturedPoints { get; }
        /// <summary>
        /// 结果的附加信息
        /// </summary>
        public string? Message { get; }

        private PlayResult(bool isSuccess, IReadOnlyList<Point>? capturedStones, string? errorMessage)
        {
            IsSuccess = isSuccess;
            CapturedPoints = capturedStones;
            Message = errorMessage;
        }
        /// <summary>
        /// 创建一个操作成功的结果
        /// </summary>
        /// <param name="capturedStones">被提的棋子列表（如果有）</param>
        /// <param name="message">成功的详细信息</param>
        /// <returns>一个成功的 PlayResult 实例</returns>
        public static PlayResult Success(IReadOnlyList<Point> capturedStones, string message)
        {
            ArgumentNullException.ThrowIfNull(capturedStones);
            return new PlayResult(true, capturedStones, message);
        }
        /// <summary>
        /// 创建一个操作失败的结果
        /// </summary>
        /// <param name="error">失败原因</param>
        /// <param name="message">失败的详细信息</param>
        /// <returns>一个失败的 PlayResult 实例</returns>
        public static PlayResult Failure(string message)
        {
            return new PlayResult(false, Array.Empty<Point>(), message);
        }
        public static PlayResult InputResult()
        {
            var capturedPoint = new List<Point>();
            Console.Write("该落子是否合法 (y/n)：");
            string c = Console.ReadLine().ToLower();
            if (!string.IsNullOrEmpty(c) && c == "y")
            {
                Console.WriteLine("请输入被提子的坐标，格式如 A1 B2 C3，若无则直接回车：");
                string capturedStr = Console.ReadLine().Trim();
                if (!string.IsNullOrEmpty(capturedStr))
                {
                    var parts = capturedStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var point = Point.TransInputToPoint(part);
                        capturedPoint.Add(point);
                    }
                }
                return PlayResult.Success(capturedPoint, "落子成功");
            }
            return PlayResult.Failure("该落子不合法");
        }
    }
}
