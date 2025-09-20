using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using PhantomGo.Core.Logic;
using PhantomGo.Core.Models;

namespace PhantomGo.Core.Helper
{
    /// <summary>
    /// 用于生成棋谱文件
    /// </summary>
    public class SgfGenerator
    {
        // 比赛数据
        private readonly string _blackTeamName;
        private readonly string _whiteTeamName;
        private readonly string _winnerInfo;
        private readonly string _gameDateTimeAndLocation;
        private readonly string _eventName;
        private readonly GameController _game;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="blackTeamName">黑方队名（先手参赛队 B）</param>
        /// <param name="whiteTeamName">白方队名（后手参赛队 W）</param>
        /// <param name="winnerInfo">获胜者信息（先手胜）</param>
        /// <param name="gameDateTimeAndLocation">比赛时间、地点（2017.07.29 14:00 重庆）</param>
        /// <param name="game">包含游戏数据的控制器</param>
        /// <param name="eventName">赛事名称（2017 CCGC）</param>
        public SgfGenerator(string blackTeamName, string whiteTeamName, string winnerInfo, string gameDateTimeAndLocation, string eventName, GameController game)
        {
            _blackTeamName = blackTeamName;
            _whiteTeamName = whiteTeamName;
            _winnerInfo = winnerInfo;
            _gameDateTimeAndLocation = gameDateTimeAndLocation;
            _eventName = eventName;
            _game = game;
        }
        /// <summary>
        /// 生成文件名
        /// </summary>
        private string GenerateFileName()
        {
            return $"PG-{_blackTeamName} vs {_whiteTeamName}-{_winnerInfo}-{_gameDateTimeAndLocation}-{_eventName}.txt";
        }
        /// <summary>
        /// 生成棋谱头部信息
        /// </summary>
        private string GenerateHeader()
        {
            return $"[PG][{_blackTeamName}][{_whiteTeamName}][{_winnerInfo}][{_gameDateTimeAndLocation}][{_eventName}]";
        }
        /// <summary>
        /// 生成落子序列
        /// </summary>
        private string GenerateMoveSequence()
        {
            var moveHistory = _game.GetMoveHistory();
            var moveStrings = moveHistory.Where(record => record.point.isMove()).Select(record =>
            {
                char player = record.player == Player.Black ? 'B' : 'W';
                string pointStr = record.point.ToString();
                return $"{player}[{pointStr}]";
            });
            return string.Join(";", moveStrings);
        }
        public static string GenerateMoveSequenceTemp(List<MoveRecord> moveHistory)
        {
            var moveStrings = moveHistory.Where(record => record.point.isMove()).Select(record =>
            {
                char player = record.player == Player.Black ? 'B' : 'W';
                string pointStr = record.point.ToString();
                return $"{player}[{pointStr}]";
            });
            return string.Join(";", moveStrings);
        }
        /// <summary>
        /// 生成棋谱内容
        /// </summary>
        private string GenerateSgfContent()
        {
            var header = GenerateHeader();
            var moveSequence = GenerateMoveSequence();
            return $"({header};{moveSequence})";
        }
        public void SaveSgfToFile(string path = "")
        {
            try
            {
                if(!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                string fileName = GenerateFileName();
                foreach(char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                string fullPath = Path.Combine(path, fileName);
                string content = GenerateSgfContent();

                // 使用 GB2312 编码写入文件
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding gb2312 = Encoding.GetEncoding("GB2312");
                File.WriteAllText(fullPath, content, Encoding.UTF8);
                Console.WriteLine($"棋谱已成功保存到: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：保存棋谱文件失败。{ex.Message}");
            }
        }
    }
}
