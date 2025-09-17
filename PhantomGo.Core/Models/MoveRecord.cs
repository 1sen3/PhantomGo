using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhantomGo.Core.Models
{
    /// <summary>
    /// 记录一次完整的移动，包括玩家、落子点和结果
    /// </summary>
    public record MoveRecord(Player player, Point point, PlayResult result);
}
