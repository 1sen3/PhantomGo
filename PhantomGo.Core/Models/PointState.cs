using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 表示棋盘中一个坐标的状态
    /// </summary>
    public enum PointState : byte
    {
        /// <summary>
        /// 该点为空（无子）
        /// </summary>
        None = 0,
        /// <summary>
        /// 该点为黑子
        /// </summary>
        black = 1,
        /// <summary>
        /// 该点为白子
        /// </summary>
        white = 2,
    }
}
