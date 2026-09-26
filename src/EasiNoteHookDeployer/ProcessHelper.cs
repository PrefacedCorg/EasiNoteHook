using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EasiNoteHookDeploy
{
    /// <summary>结束白板进程、重新启动白板。</summary>
    internal static class ProcessHelper
    {
        private const string EasiNotePrefix = "EasiNote";
        private const string EasiPrefix = "easi";
        private const int ExitWaitMs = 5000;
        private const int StartPollCount = 20;
        private const int StartPollIntervalMs = 500;

        /// <summary>
        /// 本进程 PID。
        /// 必须排除自己：部署器叫 EasiNoteHookDeploy，一样以 "EasiNote" 开头，
        /// 不排除的话 KillWhiteboard 会把自己杀掉，步骤 3/4 永远不会执行。
        /// </summary>
        private static readonly int SelfPid = Process.GetCurrentProcess().Id;

        /// <summary>结束正在运行的白板，返回被结束的进程名列表。</summary>
        internal static IList<string> KillWhiteboard(bool killAllEasi)
        {
            var killed = new List<string>();
            var targets = Process.GetProcesses().Where(p => IsKillTarget(p, killAllEasi)).ToList();

            if (targets.Count == 0)
            {
                return killed;
            }

            foreach (Process process in targets)
            {
                string name = SafeName(process);
                try
                {
                    process.Kill();
                    killed.Add(name);
                    ConsoleUi.Info("      已结束 " + name + " (PID=" + SafeId(process) + ")");
                }
                catch (Exception ex)
                {
                    ConsoleUi.Warn("      结束失败 " + name + " (PID=" + SafeId(process) + ")：" + ex.Message);
                }
            }

            // 文件锁要等进程真正退出才释放，写文件前必须等
            foreach (Process process in targets)
            {
                try
                {
                    process.WaitForExit(ExitWaitMs);
                }
                catch
                {
                    // 已退出或无权等待，忽略
                }
            }

            return killed;
        }

        /// <summary>
        /// 默认只结束白板本体（EasiNote*）。EasiUpdate* / EasiUpdate3Protect / EasiScreenPerception
        /// 是希沃的更新与保护组件，杀掉会导致更新/校验异常（而且它们会自动重启），不要动。
        /// </summary>
        private static bool IsKillTarget(Process process, bool killAllEasi)
        {
            if (SafeId(process) == SelfPid)
            {
                return false;
            }

            string name = SafeName(process);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // 进程名取自可执行文件名，部署器自己也叫 EasiNote*，所以只能按 PID 排除自己
            return killAllEasi
                ? name.IndexOf(EasiPrefix, StringComparison.OrdinalIgnoreCase) >= 0
                : name.StartsWith(EasiNotePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsWhiteboardRunning()
        {
            return Process.GetProcesses().Any(IsWhiteboard);
        }

        private static bool IsWhiteboard(Process process)
        {
            // 同样要排除自己，否则进程一启动就被判定成"白板已起来了"
            if (SafeId(process) == SelfPid)
            {
                return false;
            }

            string name = SafeName(process);
            return name.Length > 0 && name.StartsWith(EasiNotePrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>插件的自建控制台开关（进程级环境变量，子进程会继承）。</summary>
        private const string ConsoleEnvVar = "EASINOTEHOOK_CONSOLE";

        /// <summary>启动白板并确认进程真的起来了，避免"看起来启动了其实没有"。</summary>
        internal static void StartWhiteboard(string exe, bool withConsole)
        {
            if (withConsole)
            {
                StartWithConsole(exe);
            }
            else if (ConsoleUi.IsElevated())
            {
                // 提权进程直接启动会让白板也以管理员身份运行，交给资源管理器以普通用户身份拉起
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + exe + "\""));
            }
            else
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }

            for (int i = 0; i < StartPollCount; i++)
            {
                Thread.Sleep(StartPollIntervalMs);
                if (IsWhiteboardRunning())
                {
                    ConsoleUi.Info("      已启动 " + exe);
                    return;
                }
            }

            ConsoleUi.Warn("      启动后未检测到 EasiNote 进程，请手动打开白板确认。");
        }

        /// <summary>
        /// 只让"这一次"启动的白板带控制台开关。
        ///
        /// 做法是把环境变量设进**本进程**，子进程直接继承 —— 不走注册表，
        /// 所以以后双击/开始菜单打开白板完全不受影响。
        ///
        /// 代价：必须直接启动而不能走 explorer.exe（ShellExecute 传不了环境变量），
        /// 因此部署器处于提权状态时白板也会以管理员身份运行。这是临时的，关掉白板重开即可。
        /// </summary>
        private static void StartWithConsole(string exe)
        {
            Environment.SetEnvironmentVariable(ConsoleEnvVar, "1");

            if (ConsoleUi.IsElevated())
            {
                ConsoleUi.Warn("      注意：--console 会直接启动白板，本次白板也是管理员身份运行（关掉重开即恢复）。");
            }

            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe),
            });
        }

        private static string SafeName(Process process)
        {
            try
            {
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int SafeId(Process process)
        {
            try
            {
                return process.Id;
            }
            catch
            {
                return 0;
            }
        }
    }
}
