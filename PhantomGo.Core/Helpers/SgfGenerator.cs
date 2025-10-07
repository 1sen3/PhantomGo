﻿﻿﻿using System;
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
        public string GenerateFileName()
        {
            // 清理文件名中的非法字符
            string safeName = $"PG-{_blackTeamName} vs {_whiteTeamName}-{_winnerInfo}-{_gameDateTimeAndLocation}-{_eventName}.txt";
            
            // 替换Windows文件名中的非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '-');
            }
            
            // 特别处理冒号（可能不在非法字符列表中但会引起问题）
            safeName = safeName.Replace(':', '-').Replace('：', '-');
            
            return safeName;
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
        public void SaveSgfToFile()
        {
            try
            {

                string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhantomGo");

                if(!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                string fileName = GenerateFileName();
                string fullPath = Path.Combine(targetPath, fileName);
                string content = GenerateSgfContent();
                
                System.Diagnostics.Debug.WriteLine($"完整文件路径: {fullPath}");
                System.Diagnostics.Debug.WriteLine($"文件内容长度: {content.Length} 字符");

                // 使用 UTF-8 编码写入文件
                File.WriteAllText(fullPath, content, Encoding.UTF8);
                
                // 验证文件是否存在
                bool fileExists = File.Exists(fullPath);
                long fileSize = fileExists ? new FileInfo(fullPath).Length : 0;
                
                string successMsg = $"棋谱已成功保存到: {fullPath}";
                Console.WriteLine(successMsg);
                System.Diagnostics.Debug.WriteLine(successMsg);
                System.Diagnostics.Debug.WriteLine($"文件存在检查: {fileExists}, 文件大小: {fileSize} 字节");
                
                // 尝试打开文件所在文件夹
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    System.Diagnostics.Debug.WriteLine("已尝试打开文件所在文件夹");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"无法打开文件夹: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"错误：保存棋谱文件失败。{ex.Message}";
                Console.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine(errorMsg);
                System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");
            }
        }
    }
}
