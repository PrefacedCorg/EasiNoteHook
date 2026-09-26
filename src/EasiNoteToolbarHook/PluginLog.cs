using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 插件诊断输出。只到控制台，永远不写日志文件。
    ///
    /// 三种情况：
    ///   1. 白板已有控制台（从命令行启动）→ 直接输出到那里；
    ///   2. 没有控制台但父进程有（cmd/PowerShell 拉起白板）→ AttachConsole 挂上去输出；
    ///   3. 两者都没有（双击/开始菜单启动，父进程是 Explorer）→ 默认静默；
    ///      设置环境变量 EASINOTEHOOK_CONSOLE=1 时会自己开一个控制台窗口显示。
    ///
    /// 第 3 种默认不开是因为会弹黑窗，干扰正常使用；只在需要确认插件有没有加载时临时打开。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class PluginLog
    {
        private const string Prefix = "[EasiNoteToolbarHook] ";
        private const uint AttachParentProcess = 0xFFFFFFFF;
        private const string AllocConsoleSwitch = "EASINOTEHOOK_CONSOLE";

        private static bool _consoleTried;

        internal static void Write(string message)
        {
            try
            {
                EnsureConsole();
                Console.WriteLine(Prefix + message);
            }
            catch
            {
                // 输出失败绝不能影响拦截逻辑
            }
        }

        private static void EnsureConsole()
        {
            if (_consoleTried)
            {
                return;
            }

            _consoleTried = true;

            if (GetConsoleWindow() != IntPtr.Zero || AttachConsole(AttachParentProcess))
            {
                BindStdOut();
                return;
            }

            // 没有可依附的控制台；仅在显式开关下自建一个（会多出一个黑窗口）
            if (ShouldAllocConsole() && AllocConsole())
            {
                BindStdOut();
                Console.WriteLine(Prefix + "已自建控制台窗口（EASINOTEHOOK_CONSOLE）");
            }
        }

        /// <summary>环境变量开关：1/true/yes/on/always 之一即开启。</summary>
        private static bool ShouldAllocConsole()
        {
            try
            {
                string? value = Environment.GetEnvironmentVariable(AllocConsoleSwitch);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                switch (value.Trim().ToLowerInvariant())
                {
                    case "1":
                    case "true":
                    case "yes":
                    case "on":
                    case "always":
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void BindStdOut()
        {
            // 控制台默认代码页是 936(GBK)，StreamWriter 默认却写 UTF-8 —— 直接写会整屏乱码。
            // 先把控制台切成 UTF-8(65001)，再用无 BOM 的 UTF-8 写，两边才对得上。
            SetConsoleOutputCP(CpUtf8);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
            {
                AutoFlush = true,
            });
        }

        private const uint CpUtf8 = 65001;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint codePage);
    }
}
