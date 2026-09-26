using System;
using System.IO;
using Microsoft.Win32;

namespace EasiNoteHookDeploy
{
    /// <summary>
    /// 本机级状态持久化。用来记住「不可逆操作是否已经在这台电脑上做过」，
    /// 免得每次部署都再问一遍、也免得重复执行无法恢复的操作。
    ///
    /// 优先写注册表 HKLM\SOFTWARE\EasiNoteHook（机器级、跟用户无关）；
    /// 写不进去时（非管理员、组策略限制）退回 %ProgramData%\EasiNoteHook 下的标记文件。
    /// 两处都读，任一有记录即视为已执行。
    /// </summary>
    internal static class MachineState
    {
        private const string RegPath = @"SOFTWARE\EasiNoteHook";
        private const string ValueName = "BigScreenSetupDoneAt";

        private static readonly string FallbackDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EasiNoteHook");

        private static readonly string FallbackFile = Path.Combine(FallbackDir, "bigscreen-setup.done");

        /// <summary>
        /// 设置/清除用户级环境变量 EASINOTEHOOK_CONSOLE。
        ///
        /// 为什么是"用户级环境变量"而不是给子进程传：为了不让白板也变成管理员运行，
        /// 启动白板走的是 explorer.exe，而 ShellExecute 起不了带自定义环境变量的进程。
        /// 写进 HKCU\Environment 后，之后不管怎么启动白板（双击、开始菜单、explorer）都带得上。
        /// </summary>
        internal static void SetConsoleEnv(bool enabled)
        {
            try
            {
                Environment.SetEnvironmentVariable(
                    "EASINOTEHOOK_CONSOLE",
                    enabled ? "1" : null,
                    EnvironmentVariableTarget.User);
            }
            catch (Exception ex)
            {
                ConsoleUi.Warn("      环境变量设置失败：" + ex.Message);
            }
        }

        /// <summary>一体机伪装 setup 是否已在本机运行过。</summary>
        internal static bool TryReadBigScreenSetup(out string stamp)
        {
            stamp = string.Empty;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
                {
                    string value = key?.GetValue(ValueName) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        stamp = value;
                        return true;
                    }
                }
            }
            catch
            {
                // 读不到就当没有记录，继续看文件兜底
            }

            try
            {
                if (File.Exists(FallbackFile))
                {
                    stamp = File.ReadAllText(FallbackFile).Trim();
                    return true;
                }
            }
            catch
            {
                // 同上
            }

            return false;
        }

        /// <summary>记下已执行。返回是否至少写成功一处。</summary>
        internal static bool MarkBigScreenSetup()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            bool ok = false;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegPath, true))
                {
                    key.SetValue(ValueName, stamp, RegistryValueKind.String);
                    ok = true;
                }
            }
            catch (Exception ex)
            {
                ConsoleUi.Warn("      注册表写入失败（将退回文件标记）：" + ex.Message);
            }

            try
            {
                Directory.CreateDirectory(FallbackDir);
                File.WriteAllText(FallbackFile, stamp);
                ok = true;
            }
            catch (Exception ex)
            {
                ConsoleUi.Warn("      文件标记写入失败：" + ex.Message);
            }

            return ok;
        }
    }
}
