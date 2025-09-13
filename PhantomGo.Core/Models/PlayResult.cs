using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示一次落子操作的结果
    /// </summary>
    public class PlayResult
    {
        /// <summary>
        /// 枚举落子失败的原因
        /// </summary>
        public enum MoveError
        {
            /// <summary>
            /// 超出边界
            /// </summary>
            PointOutOfBounds,
            /// <summary>
            /// 位置上已经有子了
            /// </summary>
            PointIsOccupied,
            /// <summary>
            /// 尝试自杀（落子后无气）
            /// </summary>
            Suicide,
            /// <summary>
            /// 打劫行为（导致棋盘状态重复）
            /// </summary>
            KoViolation,
            /// <summary>
            /// 不在回合内
            /// </summary>
            NotThisPlayersTurn,
            /// <summary>
            /// 棋局已结束
            /// </summary>
            GameOver,
        }
        /// <summary>
        /// 表示落子是否成功
        /// </summary>
        public bool IsSuccess { get; }
        /// <summary>
        /// 如果落子成功且可提子，获取被提的棋子列表。
        /// </summary>
        public IReadOnlyList<Point>? CapturedPoints { get; }
        /// <summary>
        /// 如果落子失败，获取失败原因
        /// </summary>
        public MoveError? Error { get; }
        /// <summary>
        /// 结果的附加信息
        /// </summary>
        public string? Message { get; }

        private PlayResult(bool isSuccess, IReadOnlyList<Point>? capturedStones, MoveError? error, string? errorMessage)
        {
            IsSuccess = isSuccess;
            CapturedPoints = capturedStones;
            Error = error;
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
            return new PlayResult(true, capturedStones, null, message);
        }
        /// <summary>
        /// 创建一个落子失败的结果
        /// </summary>
        /// <param name="error">失败原因</param>
        /// <param name="message">失败的详细信息</param>
        /// <returns>一个失败的 PlayResult 实例</returns>
        public static PlayResult Failure(MoveError error, string message)
        {
            return new PlayResult(false, Array.Empty<Point>(), error, message);
        }
    }
}
