using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 目标：让「桌面模式」的悬浮工具栏不再出现。
    ///
    /// 双保险编排：
    ///   1) <see cref="DesktopModePatcher"/> —— Harmony 把桌面模式启动方法 stub 掉，窗口根本不会被创建（根治）；
    ///   2) <see cref="DockToolbarWatcher"/> —— 进程内 WinEvent 钩子兜底，窗口一旦 Show 立刻隐藏（改版后仍有效）。
    ///
    /// 两者都跑：根治方案失效时兜底仍能顶住，代价只是可能有一帧闪烁。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class ToolbarKiller
    {
        private const string WatcherThreadName = "EasiNoteToolbarHook.Watchdog";
        private const string RetryThreadName = "EasiNoteToolbarHook.PatchRetry";

        /// <summary>程序集可能晚于插件加载，重试次数 × 间隔就是这个等待窗口。</summary>
        private const int RetryCount = 6;
        private const int RetryIntervalMs = 2000;

        private const string UiAssemblyName = "EasiNote.UI";

        internal static void Start()
        {
            DesktopModePatcher.TryPatch();
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            var watchdog = new Thread(DockToolbarWatcher.Run)
            {
                IsBackground = true,
                Name = WatcherThreadName,
            };
            watchdog.SetApartmentState(ApartmentState.STA);
            watchdog.Start();

            StartPatchRetryThread();
        }

        /// <summary>EasiNote.UI 加载后再试一次：插件通常早于 UI 程序集被加载。</summary>
        private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            try
            {
                if (DesktopModePatcher.IsPatched || DesktopModePatcher.HasGivenUp)
                {
                    return;
                }

                if (string.Equals(e.LoadedAssembly.GetName().Name, UiAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    DesktopModePatcher.TryPatch();
                }
            }
            catch (Exception ex)
            {
                // 事件处理器抛异常会波及宿主，插件必须自己吞掉
                PluginLog.Write("AssemblyLoad 处理异常：" + ex.Message);
            }
        }

        /// <summary>未成功 patch 时后台重试，不阻塞消息循环；确认没戏了就立刻停，不刷屏。</summary>
        private static void StartPatchRetryThread()
        {
            if (DesktopModePatcher.IsPatched || DesktopModePatcher.HasGivenUp)
            {
                return;
            }

            var retry = new Thread(() =>
            {
                for (int i = 0; i < RetryCount; i++)
                {
                    Thread.Sleep(RetryIntervalMs);

                    if (DesktopModePatcher.IsPatched)
                    {
                        return;
                    }

                    try
                    {
                        if (DesktopModePatcher.TryPatch() == PatchOutcome.PermanentFailure)
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Write("重试 patch 异常：" + ex.Message);
                    }
                }
            })
            {
                IsBackground = true,
                Name = RetryThreadName,
            };
            retry.Start();
        }
    }
}
