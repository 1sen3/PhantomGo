using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace PhantomGo.Helper
{
    public static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();
        /// <summary>
        /// 为当前应用进程分配一个新的控制台窗口
        /// </summary>
        public static void Show()
        {
            if(!AllocConsole())
            {

            } else
            {
                System.Console.WriteLine("调试控制台已准备就绪...");
            }
        }
        /// <summary>
        /// 释放当前进程的控制台窗口。
        /// </summary>
        public static void Hide()
        {
            FreeConsole();
        }
    }
}
