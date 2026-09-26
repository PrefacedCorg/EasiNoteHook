using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace EasiNoteHookDeploy
{
    /// <summary>控制台输出、暂停、提权。输出只到命令行，不写任何日志文件。</summary>
    internal static class ConsoleUi
    {
        internal static void Title(string text)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        internal static void Step(int index, int total, string title, string detail)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[" + index + "/" + total + "] " + title);
            Console.ResetColor();
            Console.WriteLine();
            if (!string.IsNullOrEmpty(detail))
            {
                Info("      " + detail);
            }
        }

        internal static void Info(string text)
        {
            Console.WriteLine(text);
        }

        internal static void Success(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        internal static void Warn(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        internal static void Error(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        /// <summary>双击启动时控制台随进程退出而关闭，必须停住让用户看到输出；从终端运行时不打扰。</summary>
        internal static void PauseIfNeeded(bool force)
        {
            if (!force && !OwnsConsoleAlone())
            {
                return;
            }

            Console.WriteLine();
            Console.Write("按回车退出...");
            Console.ReadLine();
        }

        internal static bool IsElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>以管理员身份重启自己，成功返回 true（当前进程随即退出）。</summary>
        internal static bool RelaunchElevated(IEnumerable<string> argv)
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(new ProcessStartInfo(exe, BuildArgv(argv))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildArgv(IEnumerable<string> argv)
        {
            var builder = new StringBuilder();
            foreach (string arg in argv)
            {
                builder.Append('"').Append(arg.Replace("\"", "\\\"")).Append("\" ");
            }

            return builder.ToString();
        }

        /// <summary>控制台里只有当前进程（典型情况：双击启动）时为 true。</summary>
        private static bool OwnsConsoleAlone()
        {
            try
            {
                var list = new uint[8];
                return GetConsoleProcessList(list, (uint)list.Length) == 1;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(uint[] processList, uint count);
    }
}
