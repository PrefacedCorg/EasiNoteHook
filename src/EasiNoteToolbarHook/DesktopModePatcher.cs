using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using HarmonyLib;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 根治方案：把桌面模式的启动方法 stub 掉，悬浮工具栏根本不会被创建。
    ///
    /// 白板改版后方法可能改名，所以按"最贴近窗口创建"的顺序逐个尝试，命中第一个就停。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class DesktopModePatcher
    {
        private const string HarmonyId = "icc.easinote.no-dock-toolbar";

        /// <summary>候选 stub 目标：后续版本改名时依次回退。</summary>
        private static readonly string[] PatchCandidates =
        {
            "Cvte.EasiNote.UI.DesktopMode.DesktopBoardManager:Start",
            "Cvte.EasiNote.UI.DesktopMode.PerformanceDesktopMode:Start",
        };

        private static int _patched;
        private static int _givenUp;

        internal static bool IsPatched => Volatile.Read(ref _patched) != 0;

        /// <summary>遇到过"重试也不会变好"的错误（例如拒绝访问），已放弃 stub。</summary>
        internal static bool HasGivenUp => Volatile.Read(ref _givenUp) != 0;

        /// <summary>尝试打桩；已成功或已放弃则不再做任何事，重复调用安全。</summary>
        internal static PatchOutcome TryPatch()
        {
            if (IsPatched)
            {
                return PatchOutcome.Patched;
            }

            if (HasGivenUp)
            {
                return PatchOutcome.PermanentFailure;
            }

            MethodInfo? prefix = typeof(DesktopModePatcher).GetMethod(
                nameof(StubPrefix), BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null)
            {
                return PatchOutcome.NotFound;
            }

            foreach (string candidate in PatchCandidates)
            {
                MethodInfo? target;
                try
                {
                    target = AccessTools.Method(candidate);
                }
                catch (Exception ex)
                {
                    PluginLog.Write($"查找 {candidate} 异常：{ex.GetType().Name} - {ex.Message}");
                    continue;
                }

                if (target == null)
                {
                    continue;
                }

                try
                {
                    new Harmony(HarmonyId).Patch(target, prefix: new HarmonyMethod(prefix));
                    Volatile.Write(ref _patched, 1);
                    PluginLog.Write($"Harmony stub 成功：{target.DeclaringType?.FullName}.{target.Name}");
                    return PatchOutcome.Patched;
                }
                catch (Exception ex)
                {
                    // 带上异常类型，"拒绝访问"这种必须知道具体是哪种异常才查得下去
                    PluginLog.Write($"Harmony stub 失败（{candidate}）：{ex.GetType().Name} - {ex.Message}");

                    if (IsPermanent(ex))
                    {
                        Volatile.Write(ref _givenUp, 1);
                        PluginLog.Write("stub 无权进行，已放弃重试，只靠 WinEvent 兜底隐藏工具栏。");
                        PluginLog.Write("完整异常：" + ex);
                        return PatchOutcome.PermanentFailure;
                    }
                }
            }

            PluginLog.Write("暂未命中可 stub 的桌面模式方法（等待 EasiNote.UI 加载或重试）");
            return PatchOutcome.NotFound;
        }

        /// <summary>
        /// 判断这个异常是不是"重试也没用"的。
        /// 权限/访问类错误不会随时间变好，继续重试只会刷屏。
        /// </summary>
        private static bool IsPermanent(Exception ex)
        {
            if (ex is UnauthorizedAccessException
                || ex is MethodAccessException
                || ex is MemberAccessException
                || ex is System.Security.SecurityException
                || ex is NotSupportedException
                || ex is InvalidOperationException)
            {
                return true;
            }

            // Harmony 在 JIT / 应用 stub 时若因权限不足抛
            // Win32Exception(拒绝访问, NativeErrorCode=5 ERROR_ACCESS_DENIED)，
            // 重试不会变好，直接放弃，避免刷屏。
            if (ex is System.ComponentModel.Win32Exception win32 && win32.NativeErrorCode == 5)
            {
                return true;
            }

            return false;
        }

        /// <summary>返回 false = 跳过原方法，悬浮窗/桌面模式不再启动。</summary>
        private static bool StubPrefix()
        {
            return false;
        }
    }

    /// <summary>一次打桩尝试的结果。</summary>
    internal enum PatchOutcome
    {
        /// <summary>已打桩。</summary>
        Patched,

        /// <summary>目标方法还没出现（程序集可能还没加载），值得重试。</summary>
        NotFound,

        /// <summary>遇到权限/访问类错误，重试不会变好，已放弃。</summary>
        PermanentFailure,
    }
}
