using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EasiNoteHookDeploy
{
    /// <summary>
    /// 一体机伪装 setup：运行后白板会把本机当成希沃一体机，打开直接进入大屏授课模式。
    ///
    /// 这个操作 **不可逆**，所以在启动白板前单独询问一次；
    /// 执行结果记进 <see cref="MachineState"/>，同一台电脑下次直接跳过，不再问。
    /// </summary>
    internal static class SetupRunner
    {
        /// <summary>执行询问与运行，返回一行结果文本交给调用方打印。</summary>
        internal static string Execute(DeployOptions options)
        {
            if (options.Setup == SetupChoice.Skip)
            {
                return "已跳过（--no-setup）";
            }

            if (MachineState.TryReadBigScreenSetup(out string doneAt))
            {
                return "已跳过（本机已于 " + doneAt + " 运行过，不重复执行）";
            }

            if (options.DryRun)
            {
                return "已跳过（--dry-run）";
            }

            // 不可逆操作：默认是不运行，必须明确输入 y 才执行
            if (options.Setup != SetupChoice.Run && !Confirm())
            {
                return "已跳过（未确认）";
            }

            if (!ConsoleUi.IsElevated())
            {
                return "未运行：需要管理员权限（当前 --no-elevate，请去掉该参数重试）";
            }

            string setupPath = ExtractSetup();
            ConsoleUi.Info("      运行 " + setupPath);

            int exitCode = Run(setupPath);
            if (exitCode != 0)
            {
                ConsoleUi.Warn("      setup 退出码 " + exitCode + "，可能未成功；状态不记为已执行。");
                return "已运行，退出码 " + exitCode;
            }

            MachineState.MarkBigScreenSetup();
            return "已运行完成（不可逆）";
        }

        private static bool Confirm()
        {
            Console.WriteLine();
            ConsoleUi.Warn("      ┌─ 一体机伪装 setup ─────────────────────────────");
            ConsoleUi.Warn("      │ 作用：把本机伪装成希沃一体机，白板打开直接进入大屏授课模式。");
            ConsoleUi.Warn("      │ 注意：运行后无法恢复，且需要管理员权限。");
            ConsoleUi.Warn("      └───────────────────────────────────────────────");
            Console.Write("      是否运行？不可逆操作，默认否 [y/N] ");

            string answer = (Console.ReadLine() ?? string.Empty).Trim();
            return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把内嵌的 setup 释放到临时目录（不能就地跑资源）。</summary>
        private static string ExtractSetup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "EasiNoteHook", "setup");
            Directory.CreateDirectory(dir);

            List<PayloadFile> files = EnumerateSetupPayload().ToList();
            if (files.Count == 0)
            {
                throw new DeployException("没有打包任何 payload/setup/ 文件，构建产物可能不完整。");
            }

            string exePath = null;
            foreach (PayloadFile file in files)
            {
                string target = Path.Combine(dir, file.Name);
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(file.ResourceName))
                {
                    if (source == null)
                    {
                        throw new DeployException("内嵌资源缺失：" + file.ResourceName);
                    }

                    using (FileStream dest = File.Create(target))
                    {
                        source.CopyTo(dest);
                    }
                }

                if (exePath == null && file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exePath = target;
                }
            }

            if (exePath == null)
            {
                throw new DeployException("payload/setup/ 里没有可执行的 .exe");
            }

            return exePath;
        }

        private static int Run(string setupPath)
        {
            var psi = new ProcessStartInfo(setupPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(setupPath),
            };

            using (Process process = Process.Start(psi))
            {
                // setup 可能弹向导界面，不能设超时，等用户操作完
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static IEnumerable<PayloadFile> EnumerateSetupPayload()
        {
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!resource.StartsWith(PayloadLayout.SetupPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new PayloadFile(
                    resource.Substring(PayloadLayout.SetupPrefix.Length), resource, 0);
            }
        }
    }
}
