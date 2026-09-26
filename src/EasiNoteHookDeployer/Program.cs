using System;
using System.Collections.Generic;
using System.Linq;

namespace EasiNoteHookDeploy
{
    /// <summary>
    /// EasiNote 悬浮工具栏拦截插件 · 部署工具（.NET Framework 4.7.2，部署机无需额外运行时）
    ///
    /// 流程：结束白板进程 → 释放插件文件到 &lt;白板&gt;\Main\Extensions\EasiNoteToolbarHook → 重新打开白板
    /// 卸载：--remove 删除该目录。
    /// 全部输出打印到命令行，不写任何日志文件。
    /// </summary>
    internal static class Program
    {
        private const int DeployStepCount = 5;
        private const int UninstallStepCount = 4;

        private static int Main(string[] args)
        {
            DeployOptions options = DeployOptions.Parse(args);
            if (options == null)
            {
                DeployOptions.PrintUsage();
                ConsoleUi.PauseIfNeeded(true);
                return 2;
            }

            if (options.ShowHelp)
            {
                DeployOptions.PrintUsage();
                return 0;
            }

            Console.Title = "EasiNote 悬浮工具栏拦截插件 · 部署工具";
            ConsoleUi.Title(options.Remove
                ? "=== EasiNote 悬浮工具栏拦截插件 · 卸载 ==="
                : "=== EasiNote 悬浮工具栏拦截插件 · 部署工具 ===");

            // --dry-run 不写文件，不需要管理员
            if (!ConsoleUi.IsElevated() && !options.NoElevate && !options.DryRun)
            {
                ConsoleUi.Warn("当前不是管理员，正在请求提权（会弹 UAC 授权框）...");
                if (ConsoleUi.RelaunchElevated(options.ToArgv()))
                {
                    ConsoleUi.Info("已在新窗口以管理员身份继续，请查看那里的输出。");
                    return 0;
                }

                ConsoleUi.Error("提权被取消或失败。可用管理员终端运行，或加 --no-elevate 直接执行。");
                ConsoleUi.PauseIfNeeded(options.Pause);
                return 1;
            }

            try
            {
                return options.Remove ? Uninstall(options) : Deploy(options);
            }
            catch (DeployException ex)
            {
                Console.WriteLine();
                ConsoleUi.Error("执行失败：" + ex.Message);
                ConsoleUi.PauseIfNeeded(options.Pause);
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                ConsoleUi.Error("执行失败：" + ex);
                ConsoleUi.PauseIfNeeded(options.Pause);
                return 1;
            }
        }

        private static int Deploy(DeployOptions options)
        {
            InstallInfo install = InstallLocator.Locate(options.InstallRoot);

            ConsoleUi.Step(1, DeployStepCount, "定位白板程序", install.Exe);
            ConsoleUi.Info("      版本目录：" + install.VersionFolder);
            ConsoleUi.Info("      插件目标目录：" + install.PluginDir);

            string killSkip = KillSkipReason(options);
            ConsoleUi.Step(2, DeployStepCount, "结束白板进程", killSkip);
            if (killSkip == null)
            {
                ProcessHelper.KillWhiteboard(options.KillAllEasi);
            }

            ConsoleUi.Step(3, DeployStepCount, "释放插件文件 + 替换 Main DLL", options.DryRun ? "已跳过（--dry-run）" : null);
            if (options.DryRun)
            {
                foreach (string name in PayloadExtractor.Describe())
                {
                    ConsoleUi.Info("      将写入 " + name);
                }

                ConsoleUi.Info("      将写入 " + PluginLayout.ManifestFile);
                foreach (string name in MainPatcher.Describe())
                {
                    ConsoleUi.Info("      将替换 Main\\" + name + "（原文件备份为 .bak）");
                }
            }
            else
            {
                Extract(install.PluginDir);
                ReplaceMainFiles(install.MainDir);
            }

            // 不可逆操作放在启动白板之前，且本机只问一次
            ConsoleUi.Step(4, DeployStepCount, "一体机伪装 setup（可选，不可逆）", null);
            ConsoleUi.Info("      " + SetupRunner.Execute(options));

            string restartSkip = RestartSkipReason(options);
            ConsoleUi.Step(5, DeployStepCount, "重新打开白板", restartSkip);

            // 不管这次是否重启都设置：手动启动白板时同样需要它
            ApplyConsoleSwitch(options);

            if (restartSkip == null)
            {
                ProcessHelper.StartWhiteboard(install.Exe, options.ConsoleSwitch == true);
            }

            Console.WriteLine();
            ConsoleUi.Success("完成。");
            Console.WriteLine("  验证：进入授课后最小化白板，不应再出现 550x200 悬浮工具栏。");
            Console.WriteLine("  插件诊断输出：只打印到白板进程的父控制台（从命令行启动白板时可见），不写文件。");
            ConsoleUi.PauseIfNeeded(options.Pause);
            return 0;
        }

        private static int Uninstall(DeployOptions options)
        {
            InstallInfo install = InstallLocator.Locate(options.InstallRoot);

            ConsoleUi.Step(1, UninstallStepCount, "定位白板程序", install.Exe);

            string killSkip = KillSkipReason(options);
            ConsoleUi.Step(2, UninstallStepCount, "结束白板进程", killSkip);
            if (killSkip == null)
            {
                ProcessHelper.KillWhiteboard(options.KillAllEasi);
            }

            ConsoleUi.Step(3, UninstallStepCount, "删除插件目录 + 还原 Main DLL", options.DryRun ? "已跳过（--dry-run）" : null);
            if (options.DryRun)
            {
                ConsoleUi.Info("      将删除：" + install.PluginDir);
                foreach (string name in MainPatcher.Describe())
                {
                    ConsoleUi.Info("      将还原 Main\\" + name);
                }
            }
            else
            {
                if (PayloadExtractor.Remove(install.PluginDir))
                {
                    ConsoleUi.Info("      已删除：" + install.PluginDir);
                }
                else
                {
                    ConsoleUi.Info("      未部署，跳过：" + install.PluginDir);
                }

                RestoreMainFiles(install.MainDir);
            }

            string restartSkip = RestartSkipReason(options);
            ConsoleUi.Step(4, UninstallStepCount, "重新打开白板", restartSkip);

            ApplyConsoleSwitch(options);

            if (restartSkip == null)
            {
                ProcessHelper.StartWhiteboard(install.Exe, options.ConsoleSwitch == true);
            }

            Console.WriteLine();
            ConsoleUi.Success("已卸载，悬浮工具栏恢复默认行为。");
            ConsoleUi.PauseIfNeeded(options.Pause);
            return 0;
        }

        /// <summary>返回跳过原因；不跳过时返回 null，调用方据此决定是否真的执行。</summary>
        private static string KillSkipReason(DeployOptions options)
        {
            if (options.DryRun)
            {
                return "已跳过（--dry-run：不改任何状态）";
            }

            return options.NoKill ? "已跳过（--no-kill）" : null;
        }

        /// <summary>返回跳过原因；不跳过时返回 null。</summary>
        private static string RestartSkipReason(DeployOptions options)
        {
            if (options.DryRun)
            {
                return "已跳过（--dry-run：不会启动白板）";
            }

            return options.NoRestart ? "已跳过（--no-restart）" : null;
        }

        /// <summary>
        /// --console 不写任何持久化开关：环境变量只加在部署器进程里，由它启动的那个白板继承，
        /// 以后双击打开的白板完全不受影响。
        /// --no-console 用来清理早期版本写进注册表的用户级变量（会污染双击启动）。
        /// </summary>
        private static void ApplyConsoleSwitch(DeployOptions options)
        {
            if (options.ConsoleSwitch == false)
            {
                MachineState.SetConsoleEnv(false);
                ConsoleUi.Info("      已清除用户级 EASINOTEHOOK_CONSOLE，双击打开白板不再弹控制台");
            }
        }

        private static void ReplaceMainFiles(string mainDir)
        {
            foreach (PayloadFile file in MainPatcher.Replace(mainDir))
            {
                ConsoleUi.Info("      替换 Main\\" + file.Name + "  (" + (file.Size / 1024) + " KB)");
            }
        }

        private static void RestoreMainFiles(string mainDir)
        {
            IReadOnlyList<string> restored = MainPatcher.Restore(mainDir);
            if (restored.Count == 0)
            {
                ConsoleUi.Info("      没有可还原的 Main DLL（未替换过）");
                return;
            }

            foreach (string name in restored)
            {
                ConsoleUi.Info("      已还原 Main\\" + name);
            }
        }

        private static void Extract(string pluginDir)
        {
            IReadOnlyList<PayloadFile> written = PayloadExtractor.Extract(pluginDir);
            foreach (PayloadFile file in written)
            {
                ConsoleUi.Info("      写入 " + file.Name + "  (" + (file.Size / 1024) + " KB)");
            }

            ConsoleUi.Info("      写入 " + PluginLayout.ManifestFile);
            ConsoleUi.Info("      共 " + written.Count + " 个文件，"
                + (written.Sum(f => f.Size) / 1024) + " KB");
        }
    }
}
